using Microsoft.EntityFrameworkCore;
using Otel.Web.Services;

namespace Otel.Web.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await db.Database.EnsureCreatedAsync();
        await RunSqlObjectsAsync(db);
        await EnsureJobModuleAsync(db);
        if (await db.Hotels.AnyAsync()) return;
        await SeedAsync(db);
    }


    /// <summary>database/02_objects.sql dosyasını GO ayracına göre bölüp çalıştırır.</summary>
    static async Task RunSqlObjectsAsync(AppDb db)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "database", "02_objects.sql");
        if (!File.Exists(path)) return;
        var script = await File.ReadAllTextAsync(path);
        var batches = System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (var batch in batches.Where(b => !string.IsNullOrWhiteSpace(b)))
            await db.Database.ExecuteSqlRawAsync(batch);
    }

    static async Task SeedAsync(AppDb db)
    {
        var rnd = new Random(42);
        var today = DateTime.Today;

        // Kullanıcılar
        db.Users.AddRange(
            new AppUser { Username = "admin", FullName = "Sistem Yöneticisi", Role = AppRole.Admin, PasswordHash = PasswordHasher.Hash("admin123") },
            new AppUser { Username = "mudur", FullName = "Ayşe Yılmaz", Role = AppRole.Manager, PasswordHash = PasswordHasher.Hash("mudur123") },
            new AppUser { Username = "onburo", FullName = "Mehmet Kaya", Role = AppRole.FrontDesk, PasswordHash = PasswordHasher.Hash("onburo123") },
            new AppUser { Username = "kat", FullName = "Fatma Demir", Role = AppRole.Housekeeping, PasswordHash = PasswordHasher.Hash("kat123") },
            new AppUser { Username = "muhasebe", FullName = "Ali Çelik", Role = AppRole.Accounting, PasswordHash = PasswordHasher.Hash("muhasebe123") });

        // Tesis
        var hotel = new Hotel { Name = "Grand Aegean Resort & Spa", Address = "Sahil Cad. No:12", City = "Bodrum / Muğla", Phone = "+90 252 555 00 00", Email = "info@grandaegean.com", TaxId = "1234567890", Stars = 5 };
        db.Hotels.Add(hotel);
        await db.SaveChangesAsync();
        var floors = Enumerable.Range(1, 4).Select(i => new Floor { HotelId = hotel.Id, Number = i, Name = $"{i}. Kat" }).ToList();
        db.Floors.AddRange(floors);

        var feats = new[] {
            new RoomFeature { Name = "Klima", NameEn = "Air conditioning", Icon = "❄" },
            new RoomFeature { Name = "Minibar", NameEn = "Minibar", Icon = "🍷" },
            new RoomFeature { Name = "Deniz Manzarası", NameEn = "Sea view", Icon = "🌊" },
            new RoomFeature { Name = "Balkon", NameEn = "Balcony", Icon = "🏞" },
            new RoomFeature { Name = "Jakuzi", NameEn = "Jacuzzi", Icon = "🛁" },
            new RoomFeature { Name = "Ücretsiz Wi-Fi", NameEn = "Free Wi-Fi", Icon = "📶" } };
        db.RoomFeatures.AddRange(feats);

        var types = new[] {
            new RoomType { HotelId = hotel.Id, Name = "Standart Oda", NameEn = "Standard Room", Description = "Rahat ve şık standart oda.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 2400, AreaM2 = 24, BedType = "Çift kişilik" },
            new RoomType { HotelId = hotel.Id, Name = "Deluxe Oda", NameEn = "Deluxe Room", Description = "Geniş, balkonlu deluxe oda.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 3300, AreaM2 = 32, BedType = "King" },
            new RoomType { HotelId = hotel.Id, Name = "Deniz Manzaralı Oda", NameEn = "Sea View Room", Description = "Muhteşem deniz manzarası.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 4100, AreaM2 = 34, BedType = "King" },
            new RoomType { HotelId = hotel.Id, Name = "Aile Odası", NameEn = "Family Room", Description = "4 kişilik geniş aile odası.", BaseCapacity = 3, MaxCapacity = 4, BasePrice = 4800, AreaM2 = 45, BedType = "1 çift + 2 tek" },
            new RoomType { HotelId = hotel.Id, Name = "Suit", NameEn = "Suite", Description = "Salonlu, jakuzili lüks suit.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 7500, AreaM2 = 70, BedType = "King" } };
        db.RoomTypes.AddRange(types);
        await db.SaveChangesAsync();

        int[][] featMap = { new[] { 0, 5 }, new[] { 0, 1, 3, 5 }, new[] { 0, 1, 2, 3, 5 }, new[] { 0, 1, 3, 5 }, new[] { 0, 1, 2, 3, 4, 5 } };
        for (int i = 0; i < types.Length; i++)
            foreach (var f in featMap[i]) db.RoomTypeFeatures.Add(new RoomTypeFeature { RoomTypeId = types[i].Id, FeatureId = feats[f].Id });

        // 40 oda: her katta 10
        var rooms = new List<Room>();
        for (int fl = 0; fl < 4; fl++)
            for (int n = 1; n <= 10; n++)
            {
                int ti = fl == 0 ? (n <= 6 ? 0 : 1) : fl == 1 ? (n <= 4 ? 1 : 2) : fl == 2 ? (n <= 5 ? 2 : 3) : (n <= 6 ? 3 : 4);
                rooms.Add(new Room { HotelId = hotel.Id, FloorId = floors[fl].Id, RoomTypeId = types[ti].Id, Number = $"{(fl + 1) * 100 + n}" });
            }
        db.Rooms.AddRange(rooms);

        // Sezon fiyatları (yıl boyu)
        var y = today.Year;
        foreach (var t in types)
        {
            db.SeasonalRates.Add(new SeasonalRate { RoomTypeId = t.Id, Name = "Yaz Sezonu", StartDate = new DateTime(y, 6, 1), EndDate = new DateTime(y, 9, 15), PricePerNight = Math.Round(t.BasePrice * 1.5m), ExtraAdultPrice = 900, ChildPrice = 450, MinStay = 3 });
            db.SeasonalRates.Add(new SeasonalRate { RoomTypeId = t.Id, Name = "Ara Sezon", StartDate = new DateTime(y, 4, 1), EndDate = new DateTime(y, 5, 31), PricePerNight = Math.Round(t.BasePrice * 1.15m), ExtraAdultPrice = 700, ChildPrice = 350, MinStay = 2 });
            db.SeasonalRates.Add(new SeasonalRate { RoomTypeId = t.Id, Name = "Kış Sezonu", StartDate = new DateTime(y, 10, 16), EndDate = new DateTime(y + 1, 3, 31), PricePerNight = Math.Round(t.BasePrice * 0.85m), ExtraAdultPrice = 500, ChildPrice = 250, MinStay = 1 });
        }

        // Pansiyon tipleri, fiyat planları
        var boards = new[] {
            new BoardType { Code = "RO", Name = "Sadece Oda", NameEn = "Room Only", PerPersonPerNight = 0 },
            new BoardType { Code = "BB", Name = "Oda Kahvaltı", NameEn = "Bed & Breakfast", PerPersonPerNight = 250 },
            new BoardType { Code = "HB", Name = "Yarım Pansiyon", NameEn = "Half Board", PerPersonPerNight = 650 },
            new BoardType { Code = "FB", Name = "Tam Pansiyon", NameEn = "Full Board", PerPersonPerNight = 900 },
            new BoardType { Code = "AI", Name = "Her Şey Dahil", NameEn = "All Inclusive", PerPersonPerNight = 1300 } };
        db.BoardTypes.AddRange(boards);
        await db.SaveChangesAsync();
        db.RatePlans.AddRange(
            new RatePlan { Name = "Standart Fiyat (İadeli)", BoardTypeId = boards[1].Id, PriceFactor = 1m },
            new RatePlan { Name = "Erken Rezervasyon (İadesiz)", BoardTypeId = boards[1].Id, PriceFactor = 0.85m, IsRefundable = false },
            new RatePlan { Name = "Her Şey Dahil", BoardTypeId = boards[4].Id, PriceFactor = 1m });
        db.Promotions.AddRange(
            new Promotion { Code = "ERKEN15", Name = "Erken Rezervasyon %15", DiscountPercent = 15, MinNights = 3, StartDate = today.AddMonths(-2), EndDate = today.AddMonths(6) },
            new Promotion { Code = "HAFTA10", Name = "7 Gece %10", DiscountPercent = 10, MinNights = 7, StartDate = today.AddMonths(-2), EndDate = today.AddMonths(6) });

        // Para birimleri, ödeme yöntemleri, kasa
        db.Currencies.AddRange(
            new Currency { Code = "TRY", Name = "Türk Lirası", Symbol = "₺", RateToTry = 1 },
            new Currency { Code = "EUR", Name = "Euro", Symbol = "€", RateToTry = 38.5m },
            new Currency { Code = "USD", Name = "ABD Doları", Symbol = "$", RateToTry = 34.2m },
            new Currency { Code = "GBP", Name = "İngiliz Sterlini", Symbol = "£", RateToTry = 45.1m });
        var pms = new[] {
            new PaymentMethod { Name = "Nakit", NameEn = "Cash" }, new PaymentMethod { Name = "Kredi Kartı", NameEn = "Credit Card" },
            new PaymentMethod { Name = "Havale/EFT", NameEn = "Bank Transfer" }, new PaymentMethod { Name = "Cari Hesap", NameEn = "On Account" } };
        db.PaymentMethods.AddRange(pms);
        var cash = new CashRegister { Name = "Ön Büro Kasası", Balance = 0 };
        db.CashRegisters.Add(cash);
        await db.SaveChangesAsync();

        // Kanallar
        var chNames = new (string n, string c, decimal r)[] { ("Booking.com", "BOOKING", 15), ("Expedia", "EXPEDIA", 18), ("Agoda", "AGODA", 17), ("Airbnb", "AIRBNB", 3), ("Hotelbeds", "HOTELBEDS", 20), ("Hotels.com", "HOTELS", 18), ("Trip.com", "TRIP", 14), ("Google Hotel Ads", "GOOGLE", 10), ("Jolly", "JOLLY", 12), ("Tatilbudur", "TATILBUDUR", 13), ("Otelz.com", "OTELZ", 12), ("Odamax", "ODAMAX", 12), ("Web Sitesi", "WEB", 0) };
        var channels = chNames.Select((c, i) => new Channel { Name = c.n, Code = c.c, CommissionRate = c.r, IsConnected = i < 8 || c.c == "WEB", LastSync = i < 8 ? DateTime.Now.AddMinutes(-rnd.Next(1, 90)) : null }).ToList();
        db.Channels.AddRange(channels);
        db.Agencies.AddRange(
            new Agency { Name = "Sun Tours", ContactName = "Kemal Arslan", Phone = "+90 212 555 11 22", CommissionRate = 10 },
            new Agency { Name = "Blue Travel", ContactName = "Seda Aksoy", Phone = "+90 216 555 33 44", CommissionRate = 12 });

        // Ekstra hizmetler, stok
        db.ExtraServices.AddRange(
            new ExtraService { Name = "A la carte Akşam Yemeği", Category = ChargeCategory.Restaurant, Price = 850 },
            new ExtraService { Name = "Minibar", Category = ChargeCategory.Minibar, Price = 300 },
            new ExtraService { Name = "Spa Masajı (60 dk)", Category = ChargeCategory.Spa, Price = 1800, TaxRate = 20 },
            new ExtraService { Name = "Çamaşırhane", Category = ChargeCategory.Laundry, Price = 250 },
            new ExtraService { Name = "Havalimanı Transferi", Category = ChargeCategory.Transfer, Price = 1500, TaxRate = 20 });
        db.StockItems.AddRange(
            new StockItem { Name = "Havlu", Quantity = 300, MinQuantity = 100, UnitCost = 120 },
            new StockItem { Name = "Şampuan (mini)", Quantity = 800, MinQuantity = 300, UnitCost = 8 },
            new StockItem { Name = "Su 0.5L", Quantity = 40, MinQuantity = 200, UnitCost = 6 },
            new StockItem { Name = "Temizlik Malzemesi", Unit = "kutu", Quantity = 60, MinQuantity = 20, UnitCost = 90 });

        // Mesaj şablonları
        db.MessageTemplates.AddRange(
            new MessageTemplate { Name = "Rezervasyon Onayı", Type = MsgType.Email, Trigger = MsgTrigger.ReservationCreated, Language = "tr", Subject = "Rezervasyonunuz onaylandı", Body = "Sayın {Ad}, {Kod} numaralı rezervasyonunuz onaylanmıştır. Giriş: {Giris}, Çıkış: {Cikis}." },
            new MessageTemplate { Name = "Reservation Confirmation", Type = MsgType.Email, Trigger = MsgTrigger.ReservationCreated, Language = "en", Subject = "Your reservation is confirmed", Body = "Dear {Ad}, your reservation {Kod} is confirmed. Check-in: {Giris}, check-out: {Cikis}." },
            new MessageTemplate { Name = "Giriş Öncesi SMS", Type = MsgType.Sms, Trigger = MsgTrigger.PreArrival, Language = "tr", Body = "Sayın {Ad}, yarın sizi ağırlamaktan mutluluk duyacağız. Giriş saati 14:00." },
            new MessageTemplate { Name = "Çıkış Sonrası Teşekkür", Type = MsgType.Email, Trigger = MsgTrigger.PostStay, Language = "tr", Subject = "Bizi tercih ettiğiniz için teşekkürler", Body = "Sayın {Ad}, konaklamanız için teşekkür ederiz. Yorumunuzu bekliyoruz." });

        // Personel
        var emps = new[] {
            new Employee { FullName = "Fatma Demir", Department = "Housekeeping" }, new Employee { FullName = "Hatice Koç", Department = "Housekeeping" },
            new Employee { FullName = "Ramazan Şen", Department = "Teknik" }, new Employee { FullName = "Mehmet Kaya", Department = "Ön Büro" },
            new Employee { FullName = "Elif Yurt", Department = "Ön Büro" } };
        db.Employees.AddRange(emps);
        await db.SaveChangesAsync();
        foreach (var e in emps)
            for (int d = 0; d < 7; d++)
                db.Shifts.Add(new Shift { EmployeeId = e.Id, Date = today.AddDays(d), StartTime = new(rnd.Next(0, 2) == 0 ? 8 : 16, 0, 0), EndTime = new(rnd.Next(0, 2) == 0 ? 16 : 24 - 1, 0, 0) });

        // Misafirler
        string[] first = { "Ahmet", "Mehmet", "Ayşe", "Fatma", "Mustafa", "Zeynep", "Emre", "Elif", "Can", "Deniz", "Burak", "Selin", "Hakan", "Esra", "Oğuz", "Merve", "John", "Emma", "Hans", "Anna", "Pierre", "Olga", "Ivan", "Sarah" };
        string[] last = { "Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Öztürk", "Arslan", "Doğan", "Aydın", "Koç", "Smith", "Brown", "Müller", "Schmidt", "Dubois", "Ivanov", "Johnson", "Wilson" };
        string[] nats = { "TR", "TR", "TR", "TR", "DE", "GB", "RU", "FR", "NL" };
        var guests = new List<Guest>();
        for (int i = 0; i < 150; i++)
        {
            var fn = first[rnd.Next(first.Length)]; var ln = last[rnd.Next(last.Length)]; var nat = i < 16 ? "TR" : nats[rnd.Next(nats.Length)];
            var g = new Guest { FirstName = fn, LastName = ln, Email = $"{fn.ToLower().Replace("ş", "s").Replace("ç", "c")}.{ln.ToLower().Replace("ş", "s").Replace("ç", "c").Replace("ö", "o").Replace("ü", "u").Replace("ı", "i")}{i}@mail.com", Phone = $"+90 5{rnd.Next(30, 60)} {rnd.Next(100, 999)} {rnd.Next(10, 99)} {rnd.Next(10, 99)}", Nationality = nat, BirthDate = today.AddYears(-rnd.Next(22, 70)), IsVip = rnd.Next(0, 12) == 0, LoyaltyPoints = rnd.Next(0, 800) };
            g.Documents.Add(new GuestDocument { DocType = nat == "TR" ? "TC" : "Passport", DocNumber = nat == "TR" ? $"{rnd.NextInt64(10000000000, 99999999999)}" : $"P{rnd.Next(1000000, 9999999)}", IssueCountry = nat });
            guests.Add(g);
        }
        db.Guests.AddRange(guests);
        await db.SaveChangesAsync();
        db.Blacklist.Add(new BlacklistEntry { GuestId = guests[^1].Id, Reason = "Otel eşyalarına zarar verdi." });
        await db.SaveChangesAsync();

        // Rezervasyonlar: -75 .. +45 gün
        var rate = await db.SeasonalRates.ToListAsync();
        var roomsByType = rooms.GroupBy(r => r.RoomTypeId).ToDictionary(g => g.Key, g => g.ToList());
        var occupancy = new List<(int roomId, DateTime a, DateTime b)>();
        var plans = await db.RatePlans.ToListAsync();
        int seq = 1000;
        var cashRegId = cash.Id;

        for (int i = 0; i < 380; i++)
        {
            var type = types[rnd.Next(types.Length)];
            var ci = today.AddDays(rnd.Next(-75, 45));
            var nights = rnd.Next(1, 8);
            var co = ci.AddDays(nights);
            var free = roomsByType[type.Id].Where(r => !occupancy.Any(o => o.roomId == r.Id && ci < o.b && co > o.a)).ToList();
            if (free.Count == 0) continue;
            var room = free[rnd.Next(free.Count)];
            occupancy.Add((room.Id, ci, co));

            var board = boards[rnd.Next(boards.Length)];
            var adults = rnd.Next(1, 3) + (type.MaxCapacity > 3 && rnd.Next(0, 3) == 0 ? 1 : 0);
            decimal NightRate(DateTime d) => rate.FirstOrDefault(s => s.RoomTypeId == type.Id && d >= s.StartDate && d <= s.EndDate)?.PricePerNight ?? type.BasePrice;
            decimal total = 0;
            for (var d = ci; d < co; d = d.AddDays(1)) total += NightRate(d) + board.PerPersonPerNight * adults;
            var ch = rnd.Next(0, 10) < 6 ? channels[rnd.Next(channels.Count)] : null;
            ResStatus st = co <= today ? ResStatus.CheckedOut : (ci <= today ? ResStatus.CheckedIn : ResStatus.Confirmed);
            if (rnd.Next(0, 14) == 0) st = st == ResStatus.CheckedOut ? ResStatus.NoShow : ResStatus.Cancelled;

            var g = guests[rnd.Next(guests.Count - 1)];
            var res = new Reservation
            {
                Code = $"RZ{++seq}", GuestId = g.Id, ChannelId = ch?.Id, BoardTypeId = board.Id, RatePlanId = plans[rnd.Next(plans.Count)].Id,
                Status = st, CheckIn = ci, CheckOut = co, Adults = adults, Children = rnd.Next(0, 3) == 0 ? 1 : 0, TotalAmount = total,
                CreatedAt = ci.AddDays(-rnd.Next(1, 40))
            };
            res.Rooms.Add(new ReservationRoom { RoomTypeId = type.Id, RoomId = room.Id, CheckIn = ci, CheckOut = co, NightlyRate = Math.Round(total / nights), Adults = adults });
            res.Guests.Add(new ReservationGuest { GuestId = g.Id, IsPrimary = true });
            if (st != ResStatus.Cancelled && st != ResStatus.NoShow)
            {
                var folio = new Folio { IsClosed = st == ResStatus.CheckedOut };
                folio.Charges.Add(new FolioCharge { Description = $"Konaklama {nights} gece", Category = ChargeCategory.Room, Quantity = 1, UnitPrice = total, TaxRate = 10, Date = ci });
                if (st is ResStatus.CheckedIn or ResStatus.CheckedOut && rnd.Next(0, 2) == 0)
                    folio.Charges.Add(new FolioCharge { Description = "Spa Masajı", Category = ChargeCategory.Spa, Quantity = 1, UnitPrice = 1800, TaxRate = 20, Date = ci.AddDays(1) });
                if (st == ResStatus.CheckedOut || rnd.Next(0, 2) == 0)
                    folio.Payments.Add(new Payment { PaymentMethodId = pms[rnd.Next(3)].Id, CashRegisterId = cashRegId, Amount = st == ResStatus.CheckedOut ? total : Math.Round(total * 0.3m), Date = st == ResStatus.CheckedOut ? co : ci });
                res.Folio = folio;
            }
            db.Reservations.Add(res);
        }
        await db.SaveChangesAsync();

        // Kanal müsaitlik (önümüzdeki 60 gün)
        foreach (var ch in channels.Where(c => c.IsConnected))
            foreach (var t in types)
                for (int d = 0; d < 60; d++)
                    db.ChannelInventory.Add(new ChannelInventory { ChannelId = ch.Id, RoomTypeId = t.Id, Date = today.AddDays(d), Allotment = Math.Max(0, roomsByType[t.Id].Count - rnd.Next(0, 4)), Price = t.BasePrice, MinStay = 1 });
        await db.SaveChangesAsync();

        // Oda durumları, housekeeping, bakım
        var checkedInRooms = await db.ReservationRooms.Where(r => r.Reservation!.Status == ResStatus.CheckedIn).Select(r => r.RoomId).ToListAsync();
        foreach (var r in rooms.Where(r => checkedInRooms.Contains(r.Id)))
            r.Housekeeping = rnd.Next(0, 3) == 0 ? HkStatus.Dirty : HkStatus.Clean;
        var toClean = rooms.Where(r => r.Housekeeping == HkStatus.Dirty).ToList();
        foreach (var r in toClean) db.HousekeepingTasks.Add(new HousekeepingTask { RoomId = r.Id, EmployeeId = emps[rnd.Next(2)].Id, Type = TaskType.DailyClean });
        var broken = rooms[rnd.Next(rooms.Count)];
        db.MaintenanceRequests.Add(new MaintenanceRequest { RoomId = rooms[3].Id, Title = "Klima çalışmıyor", Priority = Priority.High, Description = "Misafir şikayeti" });
        db.MaintenanceRequests.Add(new MaintenanceRequest { RoomId = rooms[17].Id, Title = "Duş başlığı değişimi", Priority = Priority.Low, Status = WorkStatus.Done, ResolvedAt = DateTime.Now.AddDays(-1) });
        await db.SaveChangesAsync();
    }

    public static async Task EnsureJobModuleAsync(AppDb db)
    {
        try
        {
            var createTablesSql = @"
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='JobPostings' AND xtype='U')
BEGIN
    CREATE TABLE JobPostings (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        HotelId INT NOT NULL,
        Title NVARCHAR(150) NOT NULL,
        Category NVARCHAR(50) NOT NULL,
        PositionName NVARCHAR(80) NOT NULL,
        City NVARCHAR(80) NOT NULL,
        SalaryRange NVARCHAR(100) NOT NULL,
        WorkType NVARCHAR(100) NOT NULL,
        AccommodationProvided BIT NOT NULL,
        MealsIncluded BIT NOT NULL,
        ExperienceRequired NVARCHAR(100) NOT NULL,
        Description NVARCHAR(MAX) NOT NULL,
        Requirements NVARCHAR(MAX) NOT NULL,
        ContactPhone NVARCHAR(50) NOT NULL,
        ContactEmail NVARCHAR(100) NULL,
        IsActive BIT NOT NULL,
        CreatedAt DATETIME2 NOT NULL
    );
END;

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='JobApplications' AND xtype='U')
BEGIN
    CREATE TABLE JobApplications (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        JobPostingId INT NOT NULL,
        FullName NVARCHAR(100) NOT NULL,
        Phone NVARCHAR(30) NOT NULL,
        Email NVARCHAR(100) NULL,
        CurrentCity NVARCHAR(80) NOT NULL,
        ExperienceYears NVARCHAR(100) NOT NULL,
        RoleApplied NVARCHAR(50) NOT NULL,
        NeedsAccommodation BIT NOT NULL,
        Notes NVARCHAR(1000) NOT NULL,
        Status NVARCHAR(30) NOT NULL,
        AppliedAt DATETIME2 NOT NULL
    );
END;

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='JobCandidates' AND xtype='U')
BEGIN
    CREATE TABLE JobCandidates (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FullName NVARCHAR(100) NOT NULL,
        Phone NVARCHAR(30) NOT NULL,
        City NVARCHAR(80) NOT NULL,
        PreferredRole NVARCHAR(80) NOT NULL,
        ExperienceYears NVARCHAR(100) NOT NULL,
        NeedsAccommodation BIT NOT NULL,
        ReadyImmediate BIT NOT NULL,
        Note NVARCHAR(500) NOT NULL,
        CreatedAt DATETIME2 NOT NULL
    );
END;
";
            await db.Database.ExecuteSqlRawAsync(createTablesSql);

            if (!await db.JobPostings.AnyAsync())
            {
                var hotels = await db.Hotels.ToListAsync();
                if (hotels.Count == 0) return;

                var bodrumHotel = hotels.FirstOrDefault(h => h.City.Contains("Bodrum")) ?? hotels.First();
                var antalyaHotel = hotels.FirstOrDefault(h => h.City.Contains("Antalya")) ?? bodrumHotel;
                var istanbulHotel = hotels.FirstOrDefault(h => h.City.Contains("İstanbul") || h.City.Contains("Istanbul")) ?? bodrumHotel;
                var kapadokyaHotel = hotels.FirstOrDefault(h => h.City.Contains("Kapadokya") || h.City.Contains("Nevşehir")) ?? bodrumHotel;
                var cesmeHotel = hotels.FirstOrDefault(h => h.City.Contains("Çeşme") || h.City.Contains("İzmir")) ?? bodrumHotel;

                var postings = new List<JobPosting>
                {
                    new()
                    {
                        HotelId = bodrumHotel.Id,
                        Title = "A la Carte & Restoran Servis Garsonu",
                        Category = JobRoleCategory.Waiter,
                        PositionName = "Garson (Servis Elemanı)",
                        City = "Bodrum / Muğla",
                        SalaryRange = "34.000 ₺ - 42.000 ₺ + Tip / Bahşiş",
                        WorkType = "Sezonluk (Mayıs - Ekim)",
                        AccommodationProvided = true,
                        MealsIncluded = true,
                        ExperienceRequired = "En az 1 yıl otel veya fine-dining deneyimi",
                        Description = "5 yıldızlı tesisimizin deniz kenarı A la Carte balık ve ana restoranında görev alacak, misafir memnuniyeti odaklı garsonlar aranmaktadır.",
                        Requirements = "Diksiyonu düzgün, temel seviye İngilizce bilen, takım çalışmasına yatkın, güler yüzlü.",
                        ContactPhone = "+90 252 555 01 10",
                        ContactEmail = "ik@grandaegean.com",
                        IsActive = true
                    },
                    new()
                    {
                        HotelId = bodrumHotel.Id,
                        Title = "Restoran ve Bar Komisi (Yetiştirilmek Üzere)",
                        Category = JobRoleCategory.Busboy,
                        PositionName = "Komi (Garson Yardımcısı)",
                        City = "Bodrum / Muğla",
                        SalaryRange = "28.500 ₺ - 33.000 ₺ + Tip",
                        WorkType = "Sezonluk (Mayıs - Ekim)",
                        AccommodationProvided = true,
                        MealsIncluded = true,
                        ExperienceRequired = "Deneyimli veya Yetiştirilmek Üzere",
                        Description = "Salon ve bar servisinde garsonlarımıza destek olacak, masa düzeni, kuver ve tepsi servisi yapacak enerjik çalışma arkadaşları arıyoruz.",
                        Requirements = "Turizm sektöründe kariyer hedefleyen, öğrenmeye açık, genç ve dinamik.",
                        ContactPhone = "+90 252 555 01 11",
                        ContactEmail = "ik@grandaegean.com",
                        IsActive = true
                    },
                    new()
                    {
                        HotelId = bodrumHotel.Id,
                        Title = "Kat Hizmetleri Temizlik Görevlisi (Housekeeping)",
                        Category = JobRoleCategory.Housekeeping,
                        PositionName = "Temizlik Görevlisi / Kat Hizmetleri",
                        City = "Bodrum / Muğla",
                        SalaryRange = "30.000 ₺ - 35.000 ₺ + Prim",
                        WorkType = "Tam Zamanlı (6 Ay veya 12 Ay)",
                        AccommodationProvided = true,
                        MealsIncluded = true,
                        ExperienceRequired = "En az 1 sezon otel kat temizliği deneyimi",
                        Description = "Misafir odalarının ve genel alanların 5 yıldızlı hijyen standartlarına uygun olarak günlük temizliği ve düzenini sağlayacak kat görevlileri arıyoruz.",
                        Requirements = "Hijyen kurallarına ve kimyasal kullanımına hakim, titiz, dürüst, sorumluluk sahibi.",
                        ContactPhone = "+90 252 555 01 12",
                        ContactEmail = "ik@grandaegean.com",
                        IsActive = true
                    },
                    new()
                    {
                        HotelId = antalyaHotel.Id,
                        Title = "Ana Restoran & Ziyafet Garsonu",
                        Category = JobRoleCategory.Waiter,
                        PositionName = "Garson",
                        City = "Antalya / Lara",
                        SalaryRange = "32.000 ₺ - 38.000 ₺ + Tip",
                        WorkType = "Sezonluk / Tam Zamanlı",
                        AccommodationProvided = true,
                        MealsIncluded = true,
                        ExperienceRequired = "1-2 yıl otel restoran deneyimi",
                        Description = "Ultra her şey dahil resort tesisimizde açık büfe ve tematik restoran servislerinde görevlendirilecek servis personeli.",
                        Requirements = "Kişisel bakımına özen gösteren, dinamik, vardiyalı çalışabilecek.",
                        ContactPhone = "+90 242 777 00 21",
                        ContactEmail = "kariyer@antalyabeachresort.com",
                        IsActive = true
                    },
                    new()
                    {
                        HotelId = antalyaHotel.Id,
                        Title = "Kat Görevlisi / Meydancı (Genel Alan Temizlik)",
                        Category = JobRoleCategory.Housekeeping,
                        PositionName = "Temizlik Görevlisi",
                        City = "Antalya / Lara",
                        SalaryRange = "29.000 ₺ - 34.000 ₺",
                        WorkType = "Tam Zamanlı",
                        AccommodationProvided = true,
                        MealsIncluded = true,
                        ExperienceRequired = "Otel temizlik veya meydancılık tecrübesi tercih sebebidir",
                        Description = "Lobi, havuz çevresi ve spa ortak alanlarının temizlik ve hijyeninden sorumlu temizlik personeli aranıyor.",
                        Requirements = "Fiziksel dayanıklılığı yüksek, ekip ruhuna sahip.",
                        ContactPhone = "+90 242 777 00 22",
                        ContactEmail = "kariyer@antalyabeachresort.com",
                        IsActive = true
                    },
                    new()
                    {
                        HotelId = antalyaHotel.Id,
                        Title = "Havuz Bar Komisi (İçecek Servisi)",
                        Category = JobRoleCategory.Busboy,
                        PositionName = "Komi",
                        City = "Antalya / Lara",
                        SalaryRange = "28.000 ₺ - 32.000 ₺ + Tip",
                        WorkType = "Sezonluk",
                        AccommodationProvided = true,
                        MealsIncluded = true,
                        ExperienceRequired = "Yetiştirilmek üzere",
                        Description = "Havuz başı ve plaj barında barmene yardımcı olacak, bardak ve buz tedariki ile masa temizliğini yürütecek komiler aranıyor.",
                        Requirements = "Tempolu çalışma ortamına uygun, güler yüzlü.",
                        ContactPhone = "+90 242 777 00 23",
                        ContactEmail = "kariyer@antalyabeachresort.com",
                        IsActive = true
                    },
                    new()
                    {
                        HotelId = istanbulHotel.Id,
                        Title = "Ön Büro Resepsiyonist (İngilizce Bilen)",
                        Category = JobRoleCategory.FrontDesk,
                        PositionName = "Resepsiyonist",
                        City = "İstanbul / Beşiktaş",
                        SalaryRange = "38.000 ₺ - 46.000 ₺",
                        WorkType = "Tam Zamanlı (12 Ay)",
                        AccommodationProvided = false,
                        MealsIncluded = true,
                        ExperienceRequired = "En az 2 yıl otel ön büro deneyimi",
                        Description = "Boğaz kıyısındaki otelimizde check-in / check-out, misafir karşılama ve KBS bildirimlerini yürütecek ön büro uzmanı.",
                        Requirements = "Çok iyi İngilizce, PMS otel yazılımı bilgisi, problem çözme yetisi.",
                        ContactPhone = "+90 212 333 44 10",
                        ContactEmail = "ik@bosphoruspalace.com",
                        IsActive = true
                    },
                    new()
                    {
                        HotelId = kapadokyaHotel.Id,
                        Title = "Kaya Odalar Kat Hizmetleri Görevlisi",
                        Category = JobRoleCategory.Housekeeping,
                        PositionName = "Temizlik Görevlisi",
                        City = "Kapadokya / Nevşehir",
                        SalaryRange = "31.000 ₺ - 36.000 ₺",
                        WorkType = "Tam Zamanlı (12 Ay)",
                        AccommodationProvided = true,
                        MealsIncluded = true,
                        ExperienceRequired = "Butik otel kat temizliği tecrübesi",
                        Description = "Cave Suites konseptindeki odalarımızın günlük temizliğini, şömine hazırlığını ve linen değişimini yapacak çalışma arkadaşı.",
                        Requirements = "Detaylara dikkat eden, misafir mahremiyetine saygılı, güvenilir.",
                        ContactPhone = "+90 384 222 11 05",
                        ContactEmail = "info@cappadociacavesuites.com",
                        IsActive = true
                    },
                    new()
                    {
                        HotelId = cesmeHotel.Id,
                        Title = "Marina Cafe & Bar Garsonu",
                        Category = JobRoleCategory.Waiter,
                        PositionName = "Garson",
                        City = "Çeşme / İzmir",
                        SalaryRange = "33.000 ₺ - 40.000 ₺ + Yüksek Tip",
                        WorkType = "Sezonluk (Nisan - Ekim)",
                        AccommodationProvided = true,
                        MealsIncluded = true,
                        ExperienceRequired = "1 yıl kafe/bar servis deneyimi",
                        Description = "Marina manzaralı butik otelimizin teras lounge barında kokteyl ve alakart servis yapacak güler yüzlü servis personeli.",
                        Requirements = "Enerjik, iletişim becerileri kuvvetli, esnek çalışma saatlerine uyumlu.",
                        ContactPhone = "+90 232 888 77 40",
                        ContactEmail = "ik@cesmemarinahotel.com",
                        IsActive = true
                    }
                };
                db.JobPostings.AddRange(postings);
                await db.SaveChangesAsync();

                var p1 = postings[0];
                var p2 = postings[1];
                var p3 = postings[2];

                db.JobApplications.AddRange(
                    new JobApplication
                    {
                        JobPostingId = p1.Id,
                        FullName = "Emre Kara",
                        Phone = "+90 532 450 11 22",
                        Email = "emre.kara98@gmail.com",
                        CurrentCity = "İzmir",
                        ExperienceYears = "2 Yıl",
                        RoleApplied = "Garson",
                        NeedsAccommodation = true,
                        Notes = "Daha önce Çeşme ve Kuşadası'nda 4 yıldızlı otellerde 2 sezon A la Carte garsonluk yaptım. İngilizce orta seviye, lojman imkanı varsa hemen başlayabilirim.",
                        Status = "Mülakat",
                        AppliedAt = DateTime.Now.AddDays(-2)
                    },
                    new JobApplication
                    {
                        JobPostingId = p2.Id,
                        FullName = "Batuhan Çetin",
                        Phone = "+90 544 321 88 99",
                        Email = "batuhan.cetin@hotmail.com",
                        CurrentCity = "Aydın",
                        ExperienceYears = "1 Yıl",
                        RoleApplied = "Komi",
                        NeedsAccommodation = true,
                        Notes = "Geçtiğimiz yaz Didim'de restoran komiliği yaptım. Çok çalışkanım, garsonluğa terfi etmek ve otelde yetişmek istiyorum.",
                        Status = "Beklemede",
                        AppliedAt = DateTime.Now.AddDays(-1)
                    },
                    new JobApplication
                    {
                        JobPostingId = p3.Id,
                        FullName = "Meryem Şahin",
                        Phone = "+90 551 889 44 33",
                        CurrentCity = "Muğla / Milas",
                        ExperienceYears = "4 Yıl",
                        RoleApplied = "Temizlik Görevlisi",
                        NeedsAccommodation = false,
                        Notes = "Milas'ta oturuyorum, servis varsa gidiş geliş yapabilirim. 4 yıl boyunca Bodrum'da tatil köylerinde kat temizliği ve çamaşırhane tecrübem var.",
                        Status = "Arandı",
                        AppliedAt = DateTime.Now.AddDays(-3)
                    }
                );

                db.JobCandidates.AddRange(
                    new JobCandidatePool
                    {
                        FullName = "Yusuf Aksoy",
                        Phone = "+90 530 678 99 11",
                        City = "Antalya",
                        PreferredRole = "Garson",
                        ExperienceYears = "3 Yıl",
                        NeedsAccommodation = true,
                        ReadyImmediate = true,
                        Note = "İyi derecede Rusça ve orta seviye İngilizce. Akdeniz ve Ege'de 5 yıldızlı otellerde çalışmaya hazırım."
                    },
                    new JobCandidatePool
                    {
                        FullName = "Caner Polat",
                        Phone = "+90 539 123 45 67",
                        City = "Denizli",
                        PreferredRole = "Komi / Barmen Yardımcısı",
                        ExperienceYears = "Yetiştirilmek Üzere",
                        NeedsAccommodation = true,
                        ReadyImmediate = true,
                        Note = "Turizm meslek yüksekokulu mezunuyum. Sezon boyunca yatılı çalışabileceğim otel arıyorum."
                    },
                    new JobCandidatePool
                    {
                        FullName = "Gülseren Yıldız",
                        Phone = "+90 542 987 65 43",
                        City = "İzmir",
                        PreferredRole = "Kat Hizmetleri / Temizlik",
                        ExperienceYears = "5 Yıl",
                        NeedsAccommodation = true,
                        ReadyImmediate = true,
                        Note = "Büyük tatil köylerinde kat şefliği yardımcılığı ve kat temizliği yaptım. Titiz ve referanslıyım."
                    }
                );

                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[JobModuleInit] Hata veya atlandı: {ex.Message}");
        }
    }
}

