using Microsoft.EntityFrameworkCore;
using Otel.Web.Data;

namespace Otel.Web.Services;

public record PriceQuote(decimal RoomTotal, decimal BoardTotal, decimal Discount, decimal Total, int Nights, string? PromoMessage);

public class BookingRequest
{
    public int GuestId { get; set; }
    public int RoomTypeId { get; set; }
    public int? RoomId { get; set; }
    public int? ChannelId { get; set; }
    public int? AgencyId { get; set; }
    public int BoardTypeId { get; set; }
    public int? RatePlanId { get; set; }
    public DateTime CheckIn { get; set; } = DateTime.Today;
    public DateTime CheckOut { get; set; } = DateTime.Today.AddDays(2);
    public int Adults { get; set; } = 2;
    public int Children { get; set; }
    public string? PromoCode { get; set; }
    public string? Notes { get; set; }
    public ResStatus Status { get; set; } = ResStatus.Confirmed;
}

public class BookingService(AppDb db, AuthService auth, MessagingService msg)
{
    /// <summary>Tarih aralığında boş (rezervasyonu olmayan, kullanılabilir) odalar.</summary>
    public async Task<List<Room>> FreeRoomsAsync(DateTime ci, DateTime co, int? roomTypeId = null)
    {
        var busy = db.ReservationRooms
            .Where(rr => rr.RoomId != null
                && rr.Reservation!.Status != ResStatus.Cancelled && rr.Reservation.Status != ResStatus.NoShow && rr.Reservation.Status != ResStatus.CheckedOut
                && rr.CheckIn < co && rr.CheckOut > ci)
            .Select(rr => rr.RoomId!.Value);
        return await db.Rooms.Include(r => r.RoomType).Include(r => r.Floor)
            .Where(r => r.Status == RoomStatus.Available && !busy.Contains(r.Id) && (roomTypeId == null || r.RoomTypeId == roomTypeId))
            .OrderBy(r => r.Number).ToListAsync();
    }

    public async Task<PriceQuote> QuoteAsync(int roomTypeId, DateTime ci, DateTime co, int adults, int children, int boardTypeId, string? promo)
    {
        var nights = Math.Max(1, (co.Date - ci.Date).Days);
        var rt = await db.RoomTypes.AsNoTracking().FirstAsync(r => r.Id == roomTypeId);
        var seasons = await db.SeasonalRates.AsNoTracking().Where(s => s.RoomTypeId == roomTypeId && s.StartDate <= co && s.EndDate >= ci).ToListAsync();
        decimal room = 0;
        for (var d = ci.Date; d < co.Date; d = d.AddDays(1))
        {
            var s = seasons.Where(x => d >= x.StartDate && d <= x.EndDate).OrderByDescending(x => x.StartDate).FirstOrDefault();
            var night = s?.PricePerNight ?? rt.BasePrice;
            var extra = Math.Max(0, adults - rt.BaseCapacity);
            night += extra * (s?.ExtraAdultPrice ?? 800) + children * (s?.ChildPrice ?? 400);
            room += night;
        }
        var board = await db.BoardTypes.AsNoTracking().FirstAsync(b => b.Id == boardTypeId);
        var boardTotal = board.PerPersonPerNight * (adults + children / 2m) * nights;
        decimal discount = 0; string? pm = null;
        if (!string.IsNullOrWhiteSpace(promo))
        {
            var p = await db.Promotions.AsNoTracking().FirstOrDefaultAsync(x => x.Code == promo.Trim().ToUpper() && x.IsActive && x.StartDate <= ci && x.EndDate >= ci);
            if (p == null) pm = "invalid";
            else if (nights < p.MinNights) pm = $"min:{p.MinNights}";
            else { discount = Math.Round((room + boardTotal) * p.DiscountPercent / 100m, 2); pm = $"ok:{p.Name}"; }
        }
        return new PriceQuote(room, boardTotal, discount, room + boardTotal - discount, nights, pm);
    }

    public async Task<bool> IsBlacklistedAsync(int guestId) => await db.Blacklist.AnyAsync(b => b.GuestId == guestId);

    public async Task<Reservation> CreateAsync(BookingRequest r)
    {
        if (r.CheckOut <= r.CheckIn) throw new InvalidOperationException("Çıkış tarihi girişten sonra olmalı.");
        if (await IsBlacklistedAsync(r.GuestId)) throw new InvalidOperationException("Misafir kara listede!");
        var quote = await QuoteAsync(r.RoomTypeId, r.CheckIn, r.CheckOut, r.Adults, r.Children, r.BoardTypeId, r.PromoCode);

        int? roomId = r.RoomId;
        if (roomId == null)
        {
            var free = await FreeRoomsAsync(r.CheckIn, r.CheckOut, r.RoomTypeId);
            roomId = free.FirstOrDefault()?.Id;
            if (roomId == null) throw new InvalidOperationException("Seçilen tarihlerde boş oda yok.");
        }
        var promo = string.IsNullOrWhiteSpace(r.PromoCode) ? null : await db.Promotions.FirstOrDefaultAsync(p => p.Code == r.PromoCode.Trim().ToUpper());
        var seq = (await db.Reservations.CountAsync()) + 1001;
        var res = new Reservation
        {
            Code = $"RZ{seq}", GuestId = r.GuestId, ChannelId = r.ChannelId, AgencyId = r.AgencyId, BoardTypeId = r.BoardTypeId, RatePlanId = r.RatePlanId,
            PromotionId = quote.Discount > 0 ? promo?.Id : null, Status = r.Status, CheckIn = r.CheckIn, CheckOut = r.CheckOut,
            Adults = r.Adults, Children = r.Children, TotalAmount = quote.Total, DiscountAmount = quote.Discount, Notes = r.Notes, CreatedByUserId = auth.User?.Id
        };
        res.Rooms.Add(new ReservationRoom { RoomTypeId = r.RoomTypeId, RoomId = roomId, CheckIn = r.CheckIn, CheckOut = r.CheckOut, NightlyRate = Math.Round(quote.Total / quote.Nights), Adults = r.Adults, Children = r.Children });
        res.Guests.Add(new ReservationGuest { GuestId = r.GuestId, IsPrimary = true });
        var folio = new Folio();
        folio.Charges.Add(new FolioCharge { Description = $"Konaklama {quote.Nights} gece", Category = ChargeCategory.Room, UnitPrice = quote.Total, TaxRate = 10, Date = r.CheckIn });
        res.Folio = folio;
        db.Reservations.Add(res);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) { db.Entry(res).State = EntityState.Detached; throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message); }
        await msg.SendTemplateAsync(res.Id, MsgTrigger.ReservationCreated);
        return res;
    }

    public async Task CheckInAsync(int reservationId)
    {
        var res = await db.Reservations.Include(x => x.Rooms).ThenInclude(x => x.Room).Include(x => x.Guest).Include(x => x.Folio).FirstAsync(x => x.Id == reservationId);
        if (await IsBlacklistedAsync(res.GuestId)) throw new InvalidOperationException("Misafir kara listede!");
        res.Status = ResStatus.CheckedIn;
        foreach (var rr in res.Rooms) if (rr.Room != null) rr.Room.Housekeeping = HkStatus.Clean;
        db.KbsNotifications.Add(new KbsNotification { ReservationId = res.Id, GuestId = res.GuestId, Authority = res.Guest!.Nationality == "TR" ? "Polis (EGM)" : "Polis (Yabancı)", Status = "Sent", Response = "OK – bildirim alındı" });
        await db.SaveChangesAsync();
        await auth.AuditAsync("CheckIn", "Reservation", res.Id.ToString(), res.Code);
        await msg.SendTemplateAsync(res.Id, MsgTrigger.CheckIn);
    }

    public async Task CheckOutAsync(int reservationId)
    {
        var res = await db.Reservations.Include(x => x.Rooms).ThenInclude(x => x.Room).Include(x => x.Folio).FirstAsync(x => x.Id == reservationId);
        res.Status = ResStatus.CheckedOut;
        if (res.Folio != null) res.Folio.IsClosed = true;
        foreach (var rr in res.Rooms)
            if (rr.Room != null)
            {
                rr.Room.Housekeeping = HkStatus.Dirty;
                db.HousekeepingTasks.Add(new HousekeepingTask { RoomId = rr.Room.Id, Type = TaskType.Turnover, Notes = $"Çıkış: {res.Code}" });
            }
        var kbs = await db.KbsNotifications.Where(k => k.ReservationId == res.Id && k.Status == "Sent").ToListAsync();
        foreach (var k in kbs) { k.Status = "CheckedOut"; k.Response = "Çıkış bildirildi"; }
        await db.SaveChangesAsync();
        await auth.AuditAsync("CheckOut", "Reservation", res.Id.ToString(), res.Code);
        await msg.SendTemplateAsync(res.Id, MsgTrigger.PostStay);
    }

    public async Task CancelAsync(int reservationId)
    {
        var res = await db.Reservations.FirstAsync(x => x.Id == reservationId);
        res.Status = ResStatus.Cancelled;
        await db.SaveChangesAsync();
        await auth.AuditAsync("Cancel", "Reservation", res.Id.ToString(), res.Code);
    }

    public async Task AddChargeAsync(int folioId, string description, ChargeCategory cat, int qty, decimal unit, decimal tax)
    {
        db.FolioCharges.Add(new FolioCharge { FolioId = folioId, Description = description, Category = cat, Quantity = qty, UnitPrice = unit, TaxRate = tax });
        await db.SaveChangesAsync();
    }

    public async Task AddPaymentAsync(int folioId, decimal amount, int methodId, string? reference)
    {
        var cash = await db.CashRegisters.FirstOrDefaultAsync();
        db.Payments.Add(new Payment { FolioId = folioId, Amount = amount, PaymentMethodId = methodId, CashRegisterId = cash?.Id, Reference = reference, UserId = auth.User?.Id });
        await db.SaveChangesAsync();
    }

    public async Task<Invoice> CreateInvoiceAsync(int folioId, bool eInvoice)
    {
        var folio = await db.Folios.Include(f => f.Charges).FirstAsync(f => f.Id == folioId);
        var inv = new Invoice { FolioId = folioId, AccountId = folio.AccountId, IsEInvoice = eInvoice, Number = $"FTR{DateTime.Now:yyyyMM}{(await db.Invoices.CountAsync()) + 1:D5}" };
        foreach (var c in folio.Charges)
            inv.Lines.Add(new InvoiceLine { Description = c.Description, Quantity = c.Quantity, UnitPrice = Math.Round(c.UnitPrice / (1 + c.TaxRate / 100), 2), TaxRate = c.TaxRate });
        inv.Subtotal = inv.Lines.Sum(l => l.Quantity * l.UnitPrice);
        inv.Tax = inv.Lines.Sum(l => l.Quantity * l.UnitPrice * l.TaxRate / 100);
        inv.Total = inv.Subtotal + inv.Tax;
        db.Invoices.Add(inv);
        await db.SaveChangesAsync();
        await auth.AuditAsync("Invoice", "Invoice", inv.Id.ToString(), inv.Number);
        return inv;
    }

    public async Task<List<Reservation>> GetInHouseReservationsAsync(int hotelId)
    {
        var list = await db.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Folio)
            .Include(r => r.Rooms).ThenInclude(rm => rm.Room)
            .Where(r => (r.Status == ResStatus.CheckedIn || r.Status == ResStatus.Confirmed) && (hotelId == 0 || r.Rooms.Any(rm => rm.Room != null && rm.Room.HotelId == hotelId)))
            .OrderBy(r => r.Rooms.FirstOrDefault() != null ? r.Rooms.FirstOrDefault()!.Room!.Number : r.Id.ToString())
            .Take(25)
            .ToListAsync();

        if (list.Count == 0)
        {
            list = await db.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Folio)
                .Include(r => r.Rooms).ThenInclude(rm => rm.Room)
                .Take(15)
                .ToListAsync();
        }

        return list;
    }
}
