/* =====================================================================
   OTEL YONETIM SISTEMI - Gelismis veritabani nesneleri (MS SQL Server)
   Tablolar EF Core tarafindan olusturulur; bu script tetikleyici (trigger),
   fonksiyon, view ve stored procedure nesnelerini ekler.
   Idempotent: CREATE OR ALTER kullanir, tekrar tekrar calistirilabilir.
   ===================================================================== */

-- 1) Ayni odaya cakisan rezervasyonu (cifte rezervasyon) engelleyen trigger
CREATE OR ALTER TRIGGER trg_ReservationRooms_NoOverlap
ON ReservationRooms
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN ReservationRooms o ON o.RoomId = i.RoomId AND o.Id <> i.Id
        JOIN Reservations ro ON ro.Id = o.ReservationId
        JOIN Reservations ri ON ri.Id = i.ReservationId
        WHERE i.RoomId IS NOT NULL
          AND ro.Status NOT IN ('Cancelled','NoShow')
          AND ri.Status NOT IN ('Cancelled','NoShow')
          AND i.CheckIn < o.CheckOut AND i.CheckOut > o.CheckIn)
    BEGIN
        RAISERROR(N'Cifte rezervasyon: oda secilen tarihlerde dolu.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END
GO

-- 2) Odeme eklenince kasa bakiyesini guncelle
CREATE OR ALTER TRIGGER trg_Payments_UpdateCash
ON Payments
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE c SET c.Balance = c.Balance + s.Total
    FROM CashRegisters c
    JOIN (SELECT CashRegisterId, SUM(Amount) Total FROM inserted
          WHERE CashRegisterId IS NOT NULL GROUP BY CashRegisterId) s
      ON s.CashRegisterId = c.Id;
END
GO

-- 3) Stok hareketi eklenince stok miktarini guncelle
CREATE OR ALTER TRIGGER trg_StockMovements_UpdateQty
ON StockMovements
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE s SET s.Quantity = s.Quantity +
        CASE m.Type WHEN 'In' THEN m.Quantity WHEN 'Out' THEN -m.Quantity ELSE m.Quantity END
    FROM StockItems s
    JOIN inserted m ON m.StockItemId = s.Id;
END
GO

-- 4) Rezervasyon durum degisikliklerini denetim (audit) tablosuna yaz
CREATE OR ALTER TRIGGER trg_Reservations_Audit
ON Reservations
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO AuditLogs (UserId, Username, Action, Entity, EntityId, Details, At)
    SELECT i.CreatedByUserId, N'db-trigger',
           CASE WHEN d.Id IS NULL THEN N'Create' ELSE N'StatusChange' END,
           N'Reservation', CAST(i.Id AS NVARCHAR(40)),
           CASE WHEN d.Id IS NULL THEN N'Yeni rezervasyon ' + i.Code
                ELSE i.Code + N': ' + d.Status + N' -> ' + i.Status END,
           GETDATE()
    FROM inserted i
    LEFT JOIN deleted d ON d.Id = i.Id
    WHERE d.Id IS NULL OR d.Status <> i.Status;
END
GO

-- 5) Konaklama fiyati: sezonluk fiyat varsa o, yoksa oda tipi taban fiyati
CREATE OR ALTER FUNCTION fn_CalcStayPrice (@RoomTypeId INT, @CheckIn DATE, @CheckOut DATE)
RETURNS DECIMAL(12,2)
AS
BEGIN
    DECLARE @total DECIMAL(12,2) = 0, @d DATE = @CheckIn;
    WHILE @d < @CheckOut
    BEGIN
        SET @total += ISNULL(
            (SELECT TOP 1 PricePerNight FROM SeasonalRates
             WHERE RoomTypeId = @RoomTypeId AND @d BETWEEN StartDate AND EndDate
             ORDER BY StartDate DESC),
            (SELECT BasePrice FROM RoomTypes WHERE Id = @RoomTypeId));
        SET @d = DATEADD(DAY, 1, @d);
    END
    RETURN @total;
END
GO

-- 6) Bos oda sayisi (oda tipi bazinda) - tarih araligi icin
CREATE OR ALTER PROCEDURE sp_CheckAvailability
    @CheckIn DATE, @CheckOut DATE, @RoomTypeId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH RoomFree AS (
        SELECT r.Id, r.RoomTypeId,
               CASE WHEN EXISTS (
                    SELECT 1 FROM ReservationRooms rr
                    JOIN Reservations res ON res.Id = rr.ReservationId
                    WHERE rr.RoomId = r.Id
                      AND res.Status NOT IN ('Cancelled','NoShow','CheckedOut')
                      AND rr.CheckIn < @CheckOut AND rr.CheckOut > @CheckIn)
                    THEN 0 ELSE 1 END AS IsFree
        FROM Rooms r
        WHERE r.Status = 'Available'
    )
    SELECT rt.Id AS RoomTypeId, rt.Name,
           COUNT(rf.Id) AS TotalRooms,
           SUM(rf.IsFree) AS AvailableRooms,
           dbo.fn_CalcStayPrice(rt.Id, @CheckIn, @CheckOut) AS StayPrice
    FROM RoomTypes rt
    JOIN RoomFree rf ON rf.RoomTypeId = rt.Id
    WHERE @RoomTypeId IS NULL OR rt.Id = @RoomTypeId
    GROUP BY rt.Id, rt.Name;
END
GO

-- 7) Gunluk doluluk fonksiyonu (tarih araligi)
CREATE OR ALTER FUNCTION fn_DailyOccupancy (@From DATE, @To DATE)
RETURNS TABLE
AS RETURN
(
    WITH Days AS (
        SELECT @From AS D
        UNION ALL SELECT DATEADD(DAY,1,D) FROM Days WHERE D < @To
    )
    SELECT d.D AS [Date],
           (SELECT COUNT(*) FROM Rooms WHERE Status <> 'OutOfOrder') AS TotalRooms,
           (SELECT COUNT(DISTINCT rr.RoomId) FROM ReservationRooms rr
              JOIN Reservations res ON res.Id = rr.ReservationId
             WHERE res.Status IN ('Confirmed','CheckedIn','CheckedOut')
               AND rr.RoomId IS NOT NULL
               AND rr.CheckIn <= d.D AND rr.CheckOut > d.D) AS OccupiedRooms,
           ISNULL((SELECT SUM(rr.NightlyRate) FROM ReservationRooms rr
              JOIN Reservations res ON res.Id = rr.ReservationId
             WHERE res.Status IN ('Confirmed','CheckedIn','CheckedOut')
               AND rr.CheckIn <= d.D AND rr.CheckOut > d.D),0) AS RoomRevenue
    FROM Days d
)
GO

-- 8) Doluluk, ADR ve RevPAR
CREATE OR ALTER VIEW vw_RevPAR
AS
SELECT [Date], TotalRooms, OccupiedRooms, RoomRevenue,
       CAST(100.0 * OccupiedRooms / NULLIF(TotalRooms,0) AS DECIMAL(6,2)) AS OccupancyPct,
       CAST(RoomRevenue / NULLIF(OccupiedRooms,0) AS DECIMAL(12,2)) AS ADR,
       CAST(RoomRevenue / NULLIF(TotalRooms,0) AS DECIMAL(12,2)) AS RevPAR
FROM fn_DailyOccupancy(DATEADD(DAY,-30,CAST(GETDATE() AS DATE)), DATEADD(DAY,30,CAST(GETDATE() AS DATE)))
GO

-- 9) Rezervasyon ozet view'i
CREATE OR ALTER VIEW vw_ReservationSummary
AS
SELECT r.Id, r.Code, g.FirstName + N' ' + g.LastName AS GuestName,
       ISNULL(c.Name, N'Direkt') AS Channel, r.Status, r.CheckIn, r.CheckOut,
       DATEDIFF(DAY, r.CheckIn, r.CheckOut) AS Nights, r.TotalAmount,
       ISNULL((SELECT SUM(p.Amount) FROM Payments p JOIN Folios f ON f.Id = p.FolioId
               WHERE f.ReservationId = r.Id),0) AS Paid
FROM Reservations r
JOIN Guests g ON g.Id = r.GuestId
LEFT JOIN Channels c ON c.Id = r.ChannelId
GO

-- 10) Gun sonu islemi
CREATE OR ALTER PROCEDURE sp_DayClosing @BusinessDate DATE, @UserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM DayClosings WHERE BusinessDate = @BusinessDate)
    BEGIN RAISERROR(N'Bu gun zaten kapatilmis.', 16, 1); RETURN; END
    BEGIN TRANSACTION;
    DECLARE @rev DECIMAL(14,2) = ISNULL((SELECT SUM(Quantity*UnitPrice) FROM FolioCharges WHERE CAST(Date AS DATE) = @BusinessDate),0);
    DECLARE @pay DECIMAL(14,2) = ISNULL((SELECT SUM(Amount) FROM Payments WHERE CAST(Date AS DATE) = @BusinessDate),0);
    DECLARE @occ INT = (SELECT OccupiedRooms FROM fn_DailyOccupancy(@BusinessDate,@BusinessDate));
    DECLARE @tot INT = (SELECT COUNT(*) FROM Rooms);
    INSERT INTO DayClosings (BusinessDate, TotalRevenue, TotalPayments, OccupiedRooms, TotalRooms, ClosedAt, ClosedByUserId)
    VALUES (@BusinessDate, @rev, @pay, @occ, @tot, GETDATE(), @UserId);
    -- Dolu odalari kirli isaretle
    UPDATE r SET Housekeeping = 'Dirty'
    FROM Rooms r
    WHERE EXISTS (SELECT 1 FROM ReservationRooms rr JOIN Reservations res ON res.Id = rr.ReservationId
                  WHERE rr.RoomId = r.Id AND res.Status = 'CheckedIn');
    COMMIT;
END
GO
