using Microsoft.EntityFrameworkCore;
using Otel.Web.Data;

namespace Otel.Web.Services;

public record NearbyStats(int TotalCount, int BarsCount, int RestaurantsCount, int ActivitiesCount, int PartnersCount, decimal AvgRating);

public class NearbyService(IDbContextFactory<AppDb> dbFactory)
{
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            await using var db = await dbFactory.CreateDbContextAsync();

            // 1. Hotels tablosunda Latitude, Longitude, ImageUrl sütunlarını garanti et
            const string alterHotelsSql = @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Hotels' AND COLUMN_NAME = 'Latitude')
BEGIN
    ALTER TABLE Hotels ADD Latitude FLOAT NULL;
    ALTER TABLE Hotels ADD Longitude FLOAT NULL;
    ALTER TABLE Hotels ADD ImageUrl NVARCHAR(500) NULL;
END";
            await db.Database.ExecuteSqlRawAsync(alterHotelsSql);

            // 2. NearbyPlaces tablosunu oluştur veya güncelle
            const string createPlacesSql = @"
IF OBJECT_ID('NearbyPlaces', 'U') IS NULL
BEGIN
    CREATE TABLE NearbyPlaces (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        HotelId INT NOT NULL,
        Name NVARCHAR(150) NOT NULL,
        Category NVARCHAR(50) NOT NULL,
        SubCategory NVARCHAR(100) NOT NULL,
        Description NVARCHAR(MAX) NOT NULL,
        DescriptionEn NVARCHAR(MAX) NOT NULL,
        DistanceMeters INT NOT NULL,
        WalkingTimeMinutes INT NOT NULL,
        Latitude FLOAT NULL,
        Longitude FLOAT NULL,
        Address NVARCHAR(300) NOT NULL,
        Phone NVARCHAR(50) NOT NULL,
        PriceLevel INT NOT NULL,
        Rating DECIMAL(3,1) NOT NULL,
        ReviewCount INT NOT NULL,
        WorkingHours NVARCHAR(100) NOT NULL,
        HotelDiscountNote NVARCHAR(250) NULL,
        HighlightFeatures NVARCHAR(500) NULL,
        Specialties NVARCHAR(500) NULL,
        ImageUrl NVARCHAR(500) NULL,
        GoogleMapsUrl NVARCHAR(500) NULL,
        Website NVARCHAR(250) NULL,
        IsPartner BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'NearbyPlaces' AND COLUMN_NAME = 'Latitude')
    BEGIN
        ALTER TABLE NearbyPlaces ADD Latitude FLOAT NULL;
        ALTER TABLE NearbyPlaces ADD Longitude FLOAT NULL;
    END
END";
            await db.Database.ExecuteSqlRawAsync(createPlacesSql);

            // 3. Çoklu Otelleri ve Zengin Bölgesel Aktiviteleri Tohumla
            await SeedHotelsAndPlacesAsync(db);

            // 4. Tüm Otellerin Oda Tiplerini, Katlarını ve Odalarını Garanti Et
            await EnsureRoomsForHotelsAsync(db);

            // 5. Tüm Otellerin Operasyonel Verilerini (Rezervasyonlar, Durumlar, Bakım, Envanter) Garanti Et
            await EnsureOperationalDataForHotelsAsync(db);

            // 5. Görsel URL'lerini en son yüksek çözünürlüklü görsellere güncelle
            const string updateImagesSql = @"
UPDATE NearbyPlaces SET ImageUrl = '/images/cappadocia_balloon.jpg' WHERE Name LIKE '%Balon%';
UPDATE NearbyPlaces SET ImageUrl = '/images/atv_safari.jpg' WHERE Name LIKE '%ATV%';
UPDATE NearbyPlaces SET ImageUrl = '/images/scuba_diving.jpg' WHERE Name LIKE '%Diving%' OR Name LIKE '%Dalış%';
UPDATE NearbyPlaces SET ImageUrl = '/images/istanbul_bosphorus.jpg' WHERE Name LIKE '%Boğaz & Mehtap%' OR Name LIKE '%Boğaz%' OR Name LIKE '%Motoryat%';
UPDATE NearbyPlaces SET ImageUrl = '/images/historical_monument.jpg' WHERE Name LIKE '%Ayasofya%' OR Name LIKE '%Bodrum Kalesi%' OR Name LIKE '%Aspendos%' OR Name LIKE '%Derinkuyu%' OR Name LIKE '%Galata%';
UPDATE NearbyPlaces SET ImageUrl = '/images/windsurf_cesme.jpg' WHERE Name LIKE '%Sörf%' OR Name LIKE '%SUP%';
UPDATE NearbyPlaces SET ImageUrl = '/images/duden_waterfall.jpg' WHERE Name LIKE '%Düden%';
UPDATE NearbyPlaces SET ImageUrl = '/images/uludag_ski.jpg' WHERE Name LIKE '%Uludağ%' OR Name LIKE '%Kayak%' OR Name LIKE '%Fondü%' OR Name LIKE '%Aprés%';
UPDATE NearbyPlaces SET ImageUrl = '/images/fethiye_lagoon.jpg' WHERE Name LIKE '%Babadağ%' OR Name LIKE '%Ölüdeniz%' OR Name LIKE '%Kelebekler%' OR Name LIKE '%Yamaç Paraşütü%';
UPDATE NearbyPlaces SET ImageUrl = '/images/mardin_mansion.jpg' WHERE Name LIKE '%Dara%' OR Name LIKE '%Cercis%' OR Name LIKE '%Mezopotamya%' OR Name LIKE '%Deyrulzafaran%' OR Name LIKE '%Mardin%' OR Name LIKE '%Bağdadi%';

UPDATE Hotels SET ImageUrl = '/images/boat_tour.jpg' WHERE City LIKE '%Bodrum%' OR City LIKE '%Muğla%';
UPDATE Hotels SET ImageUrl = '/images/istanbul_bosphorus.jpg' WHERE City LIKE '%İstanbul%';
UPDATE Hotels SET ImageUrl = '/images/duden_waterfall.jpg' WHERE City LIKE '%Antalya%';
UPDATE Hotels SET ImageUrl = '/images/cappadocia_balloon.jpg' WHERE City LIKE '%Kapadokya%' OR City LIKE '%Nevşehir%';
UPDATE Hotels SET ImageUrl = '/images/windsurf_cesme.jpg' WHERE City LIKE '%Çeşme%' OR City LIKE '%İzmir%';
UPDATE Hotels SET ImageUrl = '/images/uludag_ski.jpg' WHERE City LIKE '%Uludağ%' OR City LIKE '%Bursa%';
UPDATE Hotels SET ImageUrl = '/images/fethiye_lagoon.jpg' WHERE City LIKE '%Fethiye%';
UPDATE Hotels SET ImageUrl = '/images/mardin_mansion.jpg' WHERE City LIKE '%Mardin%';
UPDATE Hotels SET ImageUrl = '/images/paris_palace.jpg' WHERE City LIKE '%Paris%' OR City LIKE '%Fransa%';
UPDATE Hotels SET ImageUrl = '/images/london_royal.jpg' WHERE City LIKE '%Londra%' OR City LIKE '%İngiltere%';
UPDATE Hotels SET ImageUrl = '/images/newyork_skyline.jpg' WHERE City LIKE '%New York%' OR City LIKE '%ABD%';
UPDATE Hotels SET ImageUrl = '/images/dubai_resort.jpg' WHERE City LIKE '%Dubai%' OR City LIKE '%BAE%';
UPDATE Hotels SET ImageUrl = '/images/tokyo_palace.jpg' WHERE City LIKE '%Tokyo%' OR City LIKE '%Japonya%';
UPDATE Hotels SET ImageUrl = '/images/rome_palace.jpg' WHERE City LIKE '%Roma%' OR City LIKE '%İtalya%';
UPDATE Hotels SET ImageUrl = '/images/bali_resort.jpg' WHERE City LIKE '%Bali%' OR City LIKE '%Endonezya%';
UPDATE Hotels SET ImageUrl = '/images/alps_resort.jpg' WHERE City LIKE '%Alp%' OR City LIKE '%İsviçre%' OR City LIKE '%Zürih%';
";
            await db.Database.ExecuteSqlRawAsync(updateImagesSql);

            _initialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NearbyService] Init error: {ex.Message}");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<List<Hotel>> GetHotelsAsync()
    {
        await EnsureInitializedAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Hotels.AsNoTracking().OrderBy(h => h.Id).ToListAsync();
    }

    public async Task<Hotel> AddHotelAsync(Hotel hotel)
    {
        await EnsureInitializedAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Hotels.Add(hotel);
        await db.SaveChangesAsync();
        return hotel;
    }

    public async Task<List<NearbyPlace>> GetAllAsync(
        int? hotelId = null,
        string? category = null,
        string? search = null,
        int? maxDistance = null,
        int? priceLevel = null,
        bool onlyPartners = false)
    {
        await EnsureInitializedAsync();
        await using var db = await dbFactory.CreateDbContextAsync();

        var query = db.NearbyPlaces.Include(p => p.Hotel).AsNoTracking().AsQueryable();

        if (hotelId.HasValue && hotelId.Value > 0)
        {
            query = query.Where(p => p.HotelId == hotelId.Value);
        }

        if (!string.IsNullOrWhiteSpace(category) && category != "All")
        {
            query = query.Where(p => p.Category == category);
        }

        if (onlyPartners)
        {
            query = query.Where(p => p.IsPartner);
        }

        if (maxDistance.HasValue && maxDistance.Value > 0)
        {
            query = query.Where(p => p.DistanceMeters <= maxDistance.Value);
        }

        if (priceLevel.HasValue && priceLevel.Value > 0)
        {
            query = query.Where(p => p.PriceLevel == priceLevel.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(s) ||
                p.SubCategory.ToLower().Contains(s) ||
                p.Description.ToLower().Contains(s) ||
                (p.Specialties != null && p.Specialties.ToLower().Contains(s)) ||
                (p.HighlightFeatures != null && p.HighlightFeatures.ToLower().Contains(s)) ||
                (p.Hotel != null && p.Hotel.Name.ToLower().Contains(s)) ||
                (p.Hotel != null && p.Hotel.City.ToLower().Contains(s)));
        }

        return await query.OrderBy(p => p.DistanceMeters).ThenByDescending(p => p.Rating).ToListAsync();
    }

    public async Task<NearbyPlace?> GetByIdAsync(int id)
    {
        await EnsureInitializedAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.NearbyPlaces.Include(p => p.Hotel).AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<NearbyStats> GetStatsAsync(int? hotelId = null)
    {
        await EnsureInitializedAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.NearbyPlaces.AsNoTracking().AsQueryable();

        if (hotelId.HasValue && hotelId.Value > 0)
            q = q.Where(p => p.HotelId == hotelId.Value);

        var all = await q.ToListAsync();

        return new NearbyStats(
            TotalCount: all.Count,
            BarsCount: all.Count(p => p.Category == "Bar"),
            RestaurantsCount: all.Count(p => p.Category == "Restaurant"),
            ActivitiesCount: all.Count(p => p.Category == "Activity"),
            PartnersCount: all.Count(p => p.IsPartner),
            AvgRating: all.Count > 0 ? Math.Round(all.Average(p => p.Rating), 1) : 4.8m
        );
    }

    public async Task<NearbyPlace> AddAsync(NearbyPlace place)
    {
        await EnsureInitializedAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        if (place.HotelId <= 0)
        {
            var h = await db.Hotels.FirstOrDefaultAsync();
            place.HotelId = h?.Id ?? 1;
        }
        place.CreatedAt = DateTime.Now;
        db.NearbyPlaces.Add(place);
        await db.SaveChangesAsync();
        return place;
    }

    public async Task UpdateAsync(NearbyPlace place)
    {
        await EnsureInitializedAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        db.NearbyPlaces.Update(place);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await EnsureInitializedAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.NearbyPlaces.FindAsync(id);
        if (item != null)
        {
            db.NearbyPlaces.Remove(item);
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedHotelsAndPlacesAsync(AppDb db)
    {
        // 1. Mevcut veya yeni otelleri al/ekle
        var existingHotels = await db.Hotels.ToListAsync();

        var bodrumHotel = existingHotels.FirstOrDefault(h => h.City.Contains("Bodrum") || h.City.Contains("Muğla")) ?? existingHotels.FirstOrDefault();
        if (bodrumHotel != null)
        {
            bodrumHotel.Latitude ??= 37.0344;
            bodrumHotel.Longitude ??= 27.4305;
            bodrumHotel.ImageUrl ??= "/hero.jpg";
            await db.SaveChangesAsync();
        }

        var istHotel = existingHotels.FirstOrDefault(h => h.City.Contains("İstanbul"));
        if (istHotel == null)
        {
            istHotel = new Hotel
            {
                Name = "Palace Bosphorus Luxury Hotel",
                Address = "Çırağan Cad. No:32, Beşiktaş",
                City = "İstanbul / Beşiktaş",
                Phone = "+90 212 326 40 00",
                Email = "bosphorus@otelpro.com",
                TaxId = "2345678901",
                Stars = 5,
                Latitude = 41.0435,
                Longitude = 29.0175,
                ImageUrl = "/images/aegean_dining.jpg"
            };
            db.Hotels.Add(istHotel);
            await db.SaveChangesAsync();
        }

        var antHotel = existingHotels.FirstOrDefault(h => h.City.Contains("Antalya"));
        if (antHotel == null)
        {
            antHotel = new Hotel
            {
                Name = "Riviera Palms All-Inclusive Resort & Spa",
                Address = "Lara Turizm Yolu No:85, Kundu",
                City = "Antalya / Lara",
                Phone = "+90 242 352 20 00",
                Email = "riviera@otelpro.com",
                TaxId = "3456789012",
                Stars = 5,
                Latitude = 36.8578,
                Longitude = 30.8872,
                ImageUrl = "/hero.jpg"
            };
            db.Hotels.Add(antHotel);
            await db.SaveChangesAsync();
        }

        var kapHotel = existingHotels.FirstOrDefault(h => h.City.Contains("Kapadokya") || h.City.Contains("Nevşehir"));
        if (kapHotel == null)
        {
            kapHotel = new Hotel
            {
                Name = "Cave Palace Cappadocia & Suites",
                Address = "Aydınlı Mah. Çakmaklı Sk. No:4, Göreme",
                City = "Nevşehir / Kapadokya",
                Phone = "+90 384 271 25 00",
                Email = "cappadocia@otelpro.com",
                TaxId = "4567890123",
                Stars = 5,
                Latitude = 38.6431,
                Longitude = 34.8289,
                ImageUrl = "/images/sunset_bar.jpg"
            };
            db.Hotels.Add(kapHotel);
            await db.SaveChangesAsync();
        }

        var cesHotel = existingHotels.FirstOrDefault(h => h.City.Contains("Çeşme") || h.City.Contains("İzmir"));
        if (cesHotel == null)
        {
            cesHotel = new Hotel
            {
                Name = "Alaçatı Windmill Stone Hotel & Marina",
                Address = "Alaçatı Mah. 12012 Sk. No:8, Çeşme",
                City = "İzmir / Çeşme",
                Phone = "+90 232 716 90 00",
                Email = "alacati@otelpro.com",
                TaxId = "5678901234",
                Stars = 5,
                Latitude = 38.2831,
                Longitude = 26.3756,
                ImageUrl = "/images/aegean_dining.jpg"
            };
            db.Hotels.Add(cesHotel);
            await db.SaveChangesAsync();
        }

        var uluHotel = existingHotels.FirstOrDefault(h => h.City.Contains("Uludağ") || h.City.Contains("Bursa"));
        if (uluHotel == null)
        {
            uluHotel = new Hotel
            {
                Name = "Uludağ Peak Ski & Thermal Spa Resort",
                Address = "1. Gelişim Oteller Bölgesi No:14, Uludağ",
                City = "Bursa / Uludağ",
                Phone = "+90 224 285 20 00",
                Email = "uludag@otelpro.com",
                TaxId = "6789012345",
                Stars = 5,
                Latitude = 40.1030,
                Longitude = 29.1350,
                ImageUrl = "/images/uludag_ski.jpg"
            };
            db.Hotels.Add(uluHotel);
            await db.SaveChangesAsync();
        }

        var fetHotel = existingHotels.FirstOrDefault(h => h.City.Contains("Fethiye") || h.City.Contains("Ölüdeniz"));
        if (fetHotel == null)
        {
            fetHotel = new Hotel
            {
                Name = "Lycian Lagoon Boutique Resort & Villas",
                Address = "Ölüdeniz Mah. Belcekız Cad. No:22, Fethiye",
                City = "Muğla / Fethiye",
                Phone = "+90 252 617 05 00",
                Email = "fethiye@otelpro.com",
                TaxId = "7890123456",
                Stars = 5,
                Latitude = 36.5490,
                Longitude = 29.1240,
                ImageUrl = "/images/fethiye_lagoon.jpg"
            };
            db.Hotels.Add(fetHotel);
            await db.SaveChangesAsync();
        }

        var marHotel = existingHotels.FirstOrDefault(h => h.City.Contains("Mardin"));
        if (marHotel == null)
        {
            marHotel = new Hotel
            {
                Name = "Mardin Stone Mansion Historical Palace",
                Address = "Şar Mah. 239. Sok. No:12, Artuklu",
                City = "Mardin / Artuklu",
                Phone = "+90 482 212 80 00",
                Email = "mardin@otelpro.com",
                TaxId = "8901234567",
                Stars = 5,
                Latitude = 37.3130,
                Longitude = 40.7380,
                ImageUrl = "/images/mardin_mansion.jpg"
            };
            db.Hotels.Add(marHotel);
            await db.SaveChangesAsync();
        }

        var parisHotel = existingHotels.FirstOrDefault(h => h.City.Contains("Paris") || h.City.Contains("Fransa"));
        if (parisHotel == null)
        {
            parisHotel = new Hotel
            {
                Name = "Le Grand Parisian Palace & Spa",
                Address = "15 Place Vendôme, 1er Arr.",
                City = "Paris / Fransa",
                Phone = "+33 1 43 16 30 30",
                Email = "paris@otelpro.com",
                TaxId = "FR9912345678",
                Stars = 5,
                Latitude = 48.8675,
                Longitude = 2.3294,
                ImageUrl = "/images/paris_palace.jpg"
            };
            db.Hotels.Add(parisHotel);
            await db.SaveChangesAsync();
        }

        var londonHotel = existingHotels.FirstOrDefault(h => h.City.Contains("Londra") || h.City.Contains("İngiltere") || h.City.Contains("London"));
        if (londonHotel == null)
        {
            londonHotel = new Hotel
            {
                Name = "The Royal Westminster Grand Hotel",
                Address = "10 Whitehall Place, Westminster",
                City = "Londra / İngiltere",
                Phone = "+44 20 7930 8181",
                Email = "london@otelpro.com",
                TaxId = "GB8823456789",
                Stars = 5,
                Latitude = 51.5074,
                Longitude = -0.1240,
                ImageUrl = "/images/london_royal.jpg"
            };
            db.Hotels.Add(londonHotel);
            await db.SaveChangesAsync();
        }

        var nyHotel = existingHotels.FirstOrDefault(h => h.City.Contains("New York") || h.City.Contains("ABD") || h.City.Contains("Manhattan"));
        if (nyHotel == null)
        {
            nyHotel = new Hotel
            {
                Name = "The Manhattan Skyline Central Hotel",
                Address = "768 5th Ave, Central Park South",
                City = "New York / ABD",
                Phone = "+1 212 759 3000",
                Email = "newyork@otelpro.com",
                TaxId = "US7734567890",
                Stars = 5,
                Latitude = 40.7648,
                Longitude = -73.9744,
                ImageUrl = "/images/newyork_skyline.jpg"
            };
            db.Hotels.Add(nyHotel);
            await db.SaveChangesAsync();
        }

        var dubaiHotel = existingHotels.FirstOrDefault(h => h.City.Contains("Dubai") || h.City.Contains("BAE"));
        if (dubaiHotel == null)
        {
            dubaiHotel = new Hotel
            {
                Name = "Burj Mirage Luxury Desert & Marina Resort",
                Address = "Crescent Rd, The Palm Jumeirah",
                City = "Dubai / BAE",
                Phone = "+971 4 426 2000",
                Email = "dubai@otelpro.com",
                TaxId = "AE6645678901",
                Stars = 5,
                Latitude = 25.1304,
                Longitude = 55.1171,
                ImageUrl = "/images/dubai_resort.jpg"
            };
            db.Hotels.Add(dubaiHotel);
            await db.SaveChangesAsync();
        }

        var tokyoHotel = existingHotels.FirstOrDefault(h => h.City.Contains("Tokyo") || h.City.Contains("Japonya"));
        if (tokyoHotel == null)
        {
            tokyoHotel = new Hotel
            {
                Name = "Tokyo Imperial Sakura Grand Palace",
                Address = "1-1-1 Marunouchi, Chiyoda-ku",
                City = "Tokyo / Japonya",
                Phone = "+81 3 3211 5211",
                Email = "tokyo@otelpro.com",
                TaxId = "JP5556789012",
                Stars = 5,
                Latitude = 35.6852,
                Longitude = 139.7610,
                ImageUrl = "/images/tokyo_palace.jpg"
            };
            db.Hotels.Add(tokyoHotel);
            await db.SaveChangesAsync();
        }

        var romeHotel = existingHotels.FirstOrDefault(h => h.City.Contains("Roma") || h.City.Contains("İtalya") || h.City.Contains("Rome"));
        if (romeHotel == null)
        {
            romeHotel = new Hotel
            {
                Name = "Colosseum Vista Historical Boutique Palace",
                Address = "Via dei Fori Imperiali No:42, Rione Monti",
                City = "Roma / İtalya",
                Phone = "+39 06 699 4110",
                Email = "rome@otelpro.com",
                TaxId = "IT4467890123",
                Stars = 5,
                Latitude = 41.8902,
                Longitude = 12.4922,
                ImageUrl = "/images/rome_palace.jpg"
            };
            db.Hotels.Add(romeHotel);
            await db.SaveChangesAsync();
        }

        var baliHotel = existingHotels.FirstOrDefault(h => h.City.Contains("Bali") || h.City.Contains("Endonezya"));
        if (baliHotel == null)
        {
            baliHotel = new Hotel
            {
                Name = "Ubud Paradise Rainforest & Villa Retreat",
                Address = "Jl. Raya Kedewatan, Ubud, Gianyar",
                City = "Bali / Endonezya",
                Phone = "+62 361 977 577",
                Email = "bali@otelpro.com",
                TaxId = "ID3378901234",
                Stars = 5,
                Latitude = -8.5069,
                Longitude = 115.2625,
                ImageUrl = "/images/bali_resort.jpg"
            };
            db.Hotels.Add(baliHotel);
            await db.SaveChangesAsync();
        }

        var alpsHotel = existingHotels.FirstOrDefault(h => h.City.Contains("İsviçre") || h.City.Contains("Alp") || h.City.Contains("Zürih") || h.City.Contains("Swiss"));
        if (alpsHotel == null)
        {
            alpsHotel = new Hotel
            {
                Name = "Alpine Peak Chalet & Glacier Resort",
                Address = "Bahnhofstrasse & Alpine Way, Zermatt / Zürih",
                City = "Zürih & Alpler / İsviçre",
                Phone = "+41 44 220 50 00",
                Email = "alps@otelpro.com",
                TaxId = "CH2289012345",
                Stars = 5,
                Latitude = 46.0207,
                Longitude = 7.7491,
                ImageUrl = "/images/alps_resort.jpg"
            };
            db.Hotels.Add(alpsHotel);
            await db.SaveChangesAsync();
        }

        // Mevcut mekan isimlerini alarak mükerrer kayıtları önle
        var existingNames = await db.NearbyPlaces.Select(p => p.Name).ToListAsync();
        var toAdd = new List<NearbyPlace>();

        void TryAdd(NearbyPlace p)
        {
            if (!existingNames.Contains(p.Name))
            {
                toAdd.Add(p);
                existingNames.Add(p.Name);
            }
        }

        // ─── 1. BODRUM AKTİVİTELERİ & MEKANLARI ───
        if (bodrumHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = bodrumHotel.Id,
                Name = "Bodrum Koyları Özel Yat & Gulet Turu",
                Category = "Activity",
                SubCategory = "Tekne Turu & Mavi Yolculuk",
                Description = "Bodrum'un dünyaca ünlü turkuaz koylarını lüks ahşap guletlerle keşfedin. Akvaryum Koyu, Karaada Sıcak Su Mağarası ve kristal sularda yüzme molaları.",
                DistanceMeters = 400, WalkingTimeMinutes = 5, Latitude = 37.0328, Longitude = 27.4295,
                Address = "Bodrum Yat Limanı, 3 Nolu İskele, Bodrum", Phone = "+90 252 316 55 99", PriceLevel = 3, Rating = 5.0m, ReviewCount = 524,
                WorkingHours = "09:30 - 17:30 (Her Gün)", HotelDiscountNote = "Otel Misafirlerine %20 Özel İndirim & Otelden Transfer",
                HighlightFeatures = "Akvaryum Koyu, Karaada Sıcak Su Mağarası, Teknede Taze Öğle Yemeği, Şnorkel Dahil",
                Specialties = "Kaptanın Izgara Çipura & Meze Öğle Menüsü, Gün Batımı Meyve İkramı",
                ImageUrl = "/images/boat_tour.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Bodrum+Liman+Tekne+Turlari", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = bodrumHotel.Id,
                Name = "Ege Derinlikleri Scuba Diving & Dalış",
                Category = "Activity",
                SubCategory = "Su Sporları & Tüplü Dalış",
                Description = "Bodrum'un zengin sualtı faunası, mercan resifleri ve tarihi batıkları arasında PADI sertifikalı eğitmenlerle dalış turları.",
                DistanceMeters = 900, WalkingTimeMinutes = 11, Latitude = 37.0305, Longitude = 27.4410,
                Address = "Kumbahçe Mah. Dalış İskelesi, Bodrum", Phone = "+90 252 313 70 80", PriceLevel = 3, Rating = 4.9m, ReviewCount = 380,
                WorkingHours = "08:30 - 16:30", HotelDiscountNote = "Otel Misafirlerine %15 İndirim & Ücretsiz HD Sualtı Video/Fotoğraf Çekimi",
                HighlightFeatures = "PADI & CMAS Sertifikalı Eğitmenler, Resif & Batık Dalışı, Yeni Başlayanlara Deneme Dalışı",
                Specialties = "Büyük Resif & Küçük Resif Dalış Noktası, SG 115 Sahil Güvenlik Batığı Dalışı",
                ImageUrl = "/images/scuba_diving.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Bodrum+Scuba+Diving", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = bodrumHotel.Id,
                Name = "Tarihi Bodrum Kalesi & Sualtı Arkeoloji Müzesi",
                Category = "Activity",
                SubCategory = "Tarihi & Kültürel Gezi",
                Description = "15. yüzyılda Saint Jean Şövalyeleri tarafından inşa edilen görkemli kale ve dünyanın en eski batık gemi eserleriyle tarihe yolculuk.",
                DistanceMeters = 1400, WalkingTimeMinutes = 18, Latitude = 37.0318, Longitude = 27.4298,
                Address = "Çarşı Mah. Kale Caddesi, Bodrum", Phone = "+90 252 316 25 16", PriceLevel = 1, Rating = 4.8m, ReviewCount = 1450,
                WorkingHours = "09:00 - 19:00", HotelDiscountNote = "Otel Resepsiyonundan Hızlı Sırasız Giriş Bileti",
                HighlightFeatures = "Uluburun Batığı, Şövalye Kuleleri, Panoramik 360° Bodrum Körfez Manzarası",
                Specialties = "Karyalı Prenses Salonu, İngiliz Kulesi, Fransız Kulesi",
                ImageUrl = "/images/historical_monument.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Bodrum+Kalesi", IsPartner = false
            });
            TryAdd(new NearbyPlace
            {
                HotelId = bodrumHotel.Id,
                Name = "Bitez Koyu Rüzgar Sörfü (Windsurf) & SUP",
                Category = "Activity",
                SubCategory = "Su Sporları & Rüzgar Sörfü",
                Description = "Sabit rüzgarı ve dalgasız deniziyle rüzgar sörfü için Ege'nin en ideal koylarından Bitez'de başlangıç ve ileri düzey sörf deneyimi.",
                DistanceMeters = 3200, WalkingTimeMinutes = 40, Latitude = 37.0220, Longitude = 27.3820,
                Address = "Bitez Sahil Cad. No:88, Bitez, Bodrum", Phone = "+90 252 363 80 44", PriceLevel = 2, Rating = 4.8m, ReviewCount = 295,
                WorkingHours = "09:00 - 18:30", HotelDiscountNote = "Otelden Ücretsiz Servis & Derslerde %15 İndirim",
                HighlightFeatures = "Dünyaca Ünlü Rüzgar Koridoru, SUP (Stand-Up Paddle) Kiralama, Deneyimli Eğitmenler",
                Specialties = "Sabah Sakin Deniz SUP Turu, 2 Saatlik Hızlı Windsurf Başlangıç Paketi",
                ImageUrl = "/images/windsurf_cesme.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Bitez+Windsurf+Bodrum", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = bodrumHotel.Id,
                Name = "Karaada Sıcak Su Mağarası & Çamur Banyosu Tekne Gezisi",
                Category = "Activity",
                SubCategory = "Termal Doğa & Tekne Turu",
                Description = "Bodrum açıklarındaki Karaada'da şifalı doğal termal kükürtlü mağara sularında yüzme ve cilt gençleştirici çamur banyosu deneyimi.",
                DistanceMeters = 5200, WalkingTimeMinutes = 60, Latitude = 36.9850, Longitude = 27.4600,
                Address = "Karaada Termal İskelesi, Bodrum", Phone = "+90 252 316 44 22", PriceLevel = 2, Rating = 4.9m, ReviewCount = 380,
                WorkingHours = "10:00 - 18:00", HotelDiscountNote = "Otel Misafirlerine Termal Giriş Dahil & %15 Tur İndirimi",
                HighlightFeatures = "Doğal Termal Sıcak Su Mağarası, Cilt Gençleştirici Çamur Banyosu, Turkuaz Deniz Molası",
                Specialties = "Mağara İçi Doğal Kaynak Masajı, Teknede Taze Ada Çayı İkramı",
                ImageUrl = "/images/boat_tour.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Karaada+Bodrum", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = bodrumHotel.Id,
                Name = "Orak Adası Maldivler Turkuaz Koyu Yüzme Turu",
                Category = "Activity",
                SubCategory = "Günübirlik Mavi Yolculuk & Şnorkel",
                Description = "Ege'nin Maldivleri olarak bilinen, cam gibi şeffaf denizi ve beyaz kum tabanıyla büyüleyen Orak Adası'nda tam gün yüzme turu.",
                DistanceMeters = 16000, WalkingTimeMinutes = 200, Latitude = 36.9740, Longitude = 27.6010,
                Address = "Halikarnas İskelesi, Bodrum", Phone = "+90 252 316 99 10", PriceLevel = 3, Rating = 5.0m, ReviewCount = 610,
                WorkingHours = "09:00 - 18:00", HotelDiscountNote = "Otelden Lüks Servis & Şezlong Garantili Özel Gulet Rezervasyonu",
                HighlightFeatures = "20 Metre Görüş Mesafeli Kristal Deniz, Beyaz Taşlı Kumsal, Barbekü Öğle Yemeği",
                Specialties = "Şnorkelle Deniz Kaplumbağası Gözlemi, Taze Kavun & Karpuz İkramı",
                ImageUrl = "/images/boat_tour.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Orak+Adasi+Bodrum", IsPartner = true
            });
        }

        // ─── 2. İSTANBUL AKTİVİTELERİ & MEKANLARI (Palace Bosphorus) ───
        if (istHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = istHotel.Id,
                Name = "Lüks Motoryat ile Özel Boğaz & Mehtap Turu",
                Category = "Activity",
                SubCategory = "Boğaz Tekne Turu & VIP Motoryat",
                Description = "Otel iskelesinden hareket eden 20 metrelik lüks motoryat ile saraylar, yalılar ve Boğaz köprüleri arasında gün batımı ve mehtap turu.",
                DistanceMeters = 50, WalkingTimeMinutes = 1, Latitude = 41.0440, Longitude = 29.0180,
                Address = "Palace Bosphorus Özel Yat İskelesi, Beşiktaş", Phone = "+90 212 326 40 50", PriceLevel = 4, Rating = 5.0m, ReviewCount = 390,
                WorkingHours = "10:00 - 23:00", HotelDiscountNote = "Otel Misafirlerine Özel %20 İndirim & Şampanya İkramı",
                HighlightFeatures = "Özel Kaptan & Hostes, Canlı Boğaz Rehberliği, Gurme Kanepe Tabağı, Otel İskelesinden Kalkış",
                Specialties = "Dolmabahçe Sarayı & Rumeli Hisarı Önü Fotoğraf Molası, Gün Batımı Seyri",
                ImageUrl = "/images/istanbul_bosphorus.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Ciragan+Bogaz+Yat", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = istHotel.Id,
                Name = "Bebek Koyu & Boğaz Şeridi Kano & SUP Kürek Deneyimi",
                Category = "Activity",
                SubCategory = "Su Sporları & Boğaz Kürek Turu",
                Description = "Sabahın ilk ışıklarında Boğaz akıntısının sakin olduğu saatlerde profesyonel eğitmenler eşliğinde Bebek Koyu'ndan Rumeli Hisarı'na kürek turu.",
                DistanceMeters = 2500, WalkingTimeMinutes = 30, Latitude = 41.0760, Longitude = 29.0435,
                Address = "Bebek Parkı Sahili, Beşiktaş, İstanbul", Phone = "+90 212 263 70 80", PriceLevel = 3, Rating = 4.9m, ReviewCount = 310,
                WorkingHours = "07:00 - 11:30", HotelDiscountNote = "Otelden Ücretsiz Sabah Servisi & SUP Ekipmanında %20 İndirim",
                HighlightFeatures = "Gündoğumu Boğaz Sessizliği, Rumeli Hisarı Altı Kürek Rotası, GoPro Video Kaydı",
                Specialties = "Kürek Sonrası Meşhur Bebek Badem Ezmesi & Filtre Kahve İkramı",
                ImageUrl = "/images/windsurf_cesme.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Bebek+Koyu", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = istHotel.Id,
                Name = "Tarihi Kılıç Ali Paşa Geleneksel Türk Hamamı & Kese Spa",
                Category = "Activity",
                SubCategory = "Tarihi Hamam & Geleneksel Masaj",
                Description = "Mimar Sinan tarafından 1580 yılında inşa edilen tarihi kubbe altında otantik kese, köpük masajı ve Osmanlı şerbeti eşliğinde arınma ritüeli.",
                DistanceMeters = 3400, WalkingTimeMinutes = 40, Latitude = 41.0265, Longitude = 28.9815,
                Address = "Kemankeş Karamustafa Paşa Mah. Hamam Sk. No:1, Tophane", Phone = "+90 212 393 80 10", PriceLevel = 4, Rating = 4.9m, ReviewCount = 890,
                WorkingHours = "08:00 - 23:00", HotelDiscountNote = "Otel Misafirlerine Özel Rezervasyon Önceliği & Gül Aromalı Yağ Masajı Hediyesi",
                HighlightFeatures = "440 Yıllık Mimar Sinan Kubbesi, Isıtmalı Göbek Taşı, Geleneksel İpek Kese, Dinlenme Avlusu",
                Specialties = "Ev Yapımı Demirhindi Şerbeti & Lokum İkramı, Organik Zeytinyağlı Sabun Seti",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Kilic+Ali+Pasa+Hamami", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = istHotel.Id,
                Name = "Galata Kulesi & Pera Sanat/Karaköy Yürüyüş Turu",
                Category = "Activity",
                SubCategory = "Şehir Keşfi & Tasarım Atölyeleri",
                Description = "Tarihi Galata Kulesi'nden başlayarak Beyoğlu, İstiklal Caddesi ve Karaköy tasarım sokaklarını keşfeden sokak lezzetleri ve sanat turu.",
                DistanceMeters = 3800, WalkingTimeMinutes = 45, Latitude = 41.0256, Longitude = 28.9741,
                Address = "Bereketzade Mah. Büyük Hendek Cad., Beyoğlu, İstanbul", Phone = "+90 212 245 11 00", PriceLevel = 2, Rating = 4.8m, ReviewCount = 680,
                WorkingHours = "10:00 - 19:00", HotelDiscountNote = "Otel Misafirlerine Hızlı Giriş & Çatı Katı Kahve İkramı",
                HighlightFeatures = "360 Derece Tarihi Yarımada Seyri, Fransız Geçidi, Karaköy Butik Sanat Galerileri",
                Specialties = "Meşhur Karaköy Güllüoğlu Baklava Tadımı, Tarihi Pera Çikolatası",
                ImageUrl = "/images/historical_monument.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Galata+Kulesi", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = istHotel.Id,
                Name = "Tarihi Yarımada & Ayasofya VIP Rehberli Turu",
                Category = "Activity",
                SubCategory = "Tarih, Kültür & Saraylar",
                Description = "Topkapı Sarayı, Ayasofya Camii ve Yerebatan Sarnıcı'na profesyonel lisanslı rehber eşliğinde sıra beklemeden VIP kültür turu.",
                DistanceMeters = 4800, WalkingTimeMinutes = 60, Latitude = 41.0086, Longitude = 28.9802,
                Address = "Sultanahmet Meydanı, Fatih, İstanbul", Phone = "+90 212 518 10 20", PriceLevel = 2, Rating = 4.9m, ReviewCount = 1450,
                WorkingHours = "09:00 - 18:00", HotelDiscountNote = "Otel Resepsiyonundan Sıra Beklemeden Giriş Bileti & VIP Araç Transferi",
                HighlightFeatures = "Sıra Beklemeden Giriş, Lisanslı Sanat Tarihçisi Rehber, Yerebatan Sarnıcı, Kapalıçarşı Gezisi",
                Specialties = "Harem Dairesi Ziyareti, Tarihi Sultanahmet Köftecisi Öğle Yemeği Molası",
                ImageUrl = "/images/historical_monument.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Ayasofya+Sultanahmet", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = istHotel.Id,
                Name = "Prens Adaları (Büyükada & Heybeliada) Özel Yat Gezisi",
                Category = "Activity",
                SubCategory = "Ada Turu & Yüzme Molası",
                Description = "İstanbul'un tarihi ahşap köşkleriyle ünlü Adalar takımına özel lüks yat ile kaçış; Sedef Adası ve Heybeliada koylarında yüzme molası.",
                DistanceMeters = 18000, WalkingTimeMinutes = 220, Latitude = 40.8750, Longitude = 29.1300,
                Address = "Karaköy / Beşiktaş Yat Çıkışı, İstanbul", Phone = "+90 212 326 40 55", PriceLevel = 4, Rating = 4.9m, ReviewCount = 420,
                WorkingHours = "09:30 - 18:30", HotelDiscountNote = "Otel Misafirlerine %15 İndirim & Adada Elektrikli Fayton Rezervasyonu",
                HighlightFeatures = "Büyükada Tarihi Rum Yetimhanesi & Aya Yorgi Tepesi, Sedef Adası Elio Beach Club Girişi",
                Specialties = "Yatta Taze Izgara Levrek Menüsü, Adalar Dondurması İkramı",
                ImageUrl = "/images/istanbul_bosphorus.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Buyukada+Iskele", IsPartner = true
            });

            // İstanbul Seçkin Restoranlar & Barlar
            TryAdd(new NearbyPlace
            {
                HotelId = istHotel.Id,
                Name = "Sortie Bosphorus Lounge & Cocktail Bar",
                Category = "Bar",
                SubCategory = "Boğaz Manzaralı Kokteyl Bar & Lounge",
                Description = "İstanbul Boğazı'nın hemen kıyısında, 1. Köprü ışıklarına karşı ödüllü miksoloji kokteylleri ve büyüleyici gece atmosferi.",
                DistanceMeters = 700, WalkingTimeMinutes = 8, Latitude = 41.0480, Longitude = 29.0250,
                Address = "Muallim Naci Cad. No:54, Kuruçeşme, Beşiktaş", Phone = "+90 212 327 85 85", PriceLevel = 4, Rating = 4.8m, ReviewCount = 530,
                WorkingHours = "18:00 - 03:00", HotelDiscountNote = "Otel Misafirlerine %15 İndirim & Girişte Öncelikli Rezervasyon Masası",
                HighlightFeatures = "Boğaz Sıfır Manzara, Canlı DJ Performansları, İmza Kokteyller, VIP Masa Hizmeti",
                Specialties = "Bosphorus Mist Kokteyli, Somon Tartar, Truffle Fries",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Sortie+Istanbul", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = istHotel.Id,
                Name = "Sunset Grill & Bar Ulus",
                Category = "Restaurant",
                SubCategory = "Akdeniz & Çağdaş Türk Fine Dining",
                Description = "Ulus Parkı tepesinden tüm İstanbul Boğazı'na hakim, Michelin Rehberi tavsiyeli seçkin gastronomi ve şarap kavi.",
                DistanceMeters = 2400, WalkingTimeMinutes = 30, Latitude = 41.0665, Longitude = 29.0270,
                Address = "Kuruçeşme Mah. Yol Sok. No:2, Ulus Parkı İçi, Beşiktaş", Phone = "+90 212 287 03 57", PriceLevel = 4, Rating = 4.9m, ReviewCount = 780,
                WorkingHours = "12:00 - 00:00", HotelDiscountNote = "Otel Misafirlerine Şefin Hoşgeldiniz İkramı & En İyi Manzaralı Masa Garantisi",
                HighlightFeatures = "Panoramik Boğaz Manzarası, Michelin Tavsiyeli Menü, Zengin Suşi Barı, Sommelier Hizmeti",
                Specialties = "Dry-Aged Dana Bonfile, Trüflü Risotto, Taze Suşi & Saşimi Menüsü",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Sunset+Grill+Bar+Istanbul", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = istHotel.Id,
                Name = "Tarihi Karaköy Lokantası",
                Category = "Restaurant",
                SubCategory = "Geleneksel Modern Türk & Meze",
                Description = "Turkuaz çinileriyle ünlü tarihi atmosferde İstanbul'un en seçkin tencere yemekleri ve akşamları gurme meze sofrası.",
                DistanceMeters = 3800, WalkingTimeMinutes = 45, Latitude = 41.0235, Longitude = 28.9770,
                Address = "Kemankeş Cad. No:37/A, Karaköy, Beyoğlu", Phone = "+90 212 292 44 55", PriceLevel = 3, Rating = 4.9m, ReviewCount = 1120,
                WorkingHours = "12:00 - 00:00", HotelDiscountNote = "Otel Misafirlerine Tatlı & Türk Kahvesi İkramı",
                HighlightFeatures = "Tarihi Çini Dekorasyon, Günlük Meze Çeşitleri, Hünkar Beğendi, Rakı Eşleşmesi",
                Specialties = "Ahtapotlu Izgara Enginar, Hünkar Beğendi, Vişneli Yaprak Sarma",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Karakoy+Lokantasi", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = istHotel.Id,
                Name = "Lucca Bistro & Cocktail Bar Bebek",
                Category = "Bar",
                SubCategory = "Bistro Bar & Popüler Sosyalleşme",
                Description = "Bebek'in kalbinde İstanbul cemiyet hayatının buluşma noktası; ödüllü kokteyller ve gurme atıştırmalıklar.",
                DistanceMeters = 2600, WalkingTimeMinutes = 32, Latitude = 41.0770, Longitude = 29.0430,
                Address = "Cevdet Paşa Cad. No:51, Bebek, Beşiktaş", Phone = "+90 212 257 12 57", PriceLevel = 4, Rating = 4.8m, ReviewCount = 640,
                WorkingHours = "11:00 - 02:00", HotelDiscountNote = "Otel Misafirlerine Öncelikli Rezervasyon & VIP Karşılama Kokteyli",
                HighlightFeatures = "Satsuma Vodka Kokteyli, Gurme Tapas Tabağı, Açık Hava Masaları",
                Specialties = "Orijinal Satsuma Kokteyli, Ördekli Taco, Trüflü Patates",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Lucca+Bebek", IsPartner = true
            });
        }

        // ─── 3. ANTALYA AKTİVİTELERİ & MEKANLARI (Riviera Palms Resort) ───
        if (antHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = antHotel.Id,
                Name = "Lara Plajı Parasailing & Jet Ski Su Sporları",
                Category = "Activity",
                SubCategory = "Adrenalin & Deniz Paraşütü",
                Description = "Otel sahilinin hemen önünde turkuaz Akdeniz semalarında 200 metre yükseklikten parasailing ve son model jet ski heyecanı.",
                DistanceMeters = 400, WalkingTimeMinutes = 5, Latitude = 36.8530, Longitude = 30.8850,
                Address = "Lara Turizm Yolu Kundu Sahil İskelesi, Antalya", Phone = "+90 242 352 90 90", PriceLevel = 3, Rating = 4.8m, ReviewCount = 540,
                WorkingHours = "09:00 - 18:30", HotelDiscountNote = "Otel Misafirlerine %20 İndirim & Ücretsiz Aksiyon Kamerası Çekimi",
                HighlightFeatures = "200 Metre Parasailing Uçuşu, Yamaha Jet Ski Kiralama, Güvenlik Eğitimi",
                Specialties = "Denizden Toros Dağları ve Lara Sahil Şeridi Panoraması",
                ImageUrl = "/images/boat_tour.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Lara+Plaji+Su+Sporlari", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = antHotel.Id,
                Name = "Düden Şelalesi & Falezler Tekne Turu",
                Category = "Activity",
                SubCategory = "Şelale Tekne Turu & Yüzme Molası",
                Description = "Akdeniz'e 40 metreden dökülen Aşağı Düden Şelalesi'nin altına yaklaşan ve falez mağaralarını keşfeden tekne turu.",
                DistanceMeters = 3400, WalkingTimeMinutes = 40, Latitude = 36.8512, Longitude = 30.7876,
                Address = "Lara Düden Parkı Sahil İskelesi, Antalya", Phone = "+90 242 324 88 00", PriceLevel = 2, Rating = 4.9m, ReviewCount = 980,
                WorkingHours = "10:00 - 18:00", HotelDiscountNote = "Otelden Ücretsiz Servis & Biletlerde %15 İndirim",
                HighlightFeatures = "Düden Şelalesi Altında Yüzme Molası, Falez Mağaraları, Rehberli Anlatım",
                Specialties = "Şelale Altı Gökkuşağı Fotoğrafı, Taze Meyve İkramı",
                ImageUrl = "/images/duden_waterfall.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Duden+Selalesi+Tekne", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = antHotel.Id,
                Name = "Antik Aspendos Tiyatrosu & Perge Kültür Gezisi",
                Category = "Activity",
                SubCategory = "Arkeoloji & Roma Tarihi Gezisi",
                Description = "Akustiğiyle dünyanın en iyi korunmuş Roma tiyatrosu Aspendos ve sütunlu caddeleriyle büyüleyen Perge Antik Kenti rehberli turu.",
                DistanceMeters = 32000, WalkingTimeMinutes = 380, Latitude = 36.9389, Longitude = 31.1722,
                Address = "Serik / Aksu Arkeoloji Parkı, Antalya", Phone = "+90 242 735 70 67", PriceLevel = 2, Rating = 4.9m, ReviewCount = 1120,
                WorkingHours = "08:30 - 19:00", HotelDiscountNote = "Otelden Klimalı Mercedes Transfer & Hızlı Sırasız Giriş",
                HighlightFeatures = "M.S. 2. Yüzyıl Roma Tiyatrosu, Perge Helenistik Kuleleri, Tarihi Su Kemerleri",
                Specialties = "Tiyatro Sahnesinde Doğal Akustik Yankı Deneyimi",
                ImageUrl = "/images/historical_monument.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Aspendos+Antik+Tiyatrosu", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = antHotel.Id,
                Name = "Köprülü Kanyon Rafting & Zipline Macerası",
                Category = "Activity",
                SubCategory = "Doğa Sporları & Nehir Rafting",
                Description = "Toros Dağları'nın serin kanyon sularında 14 kilometrelik rafting parkuru, kanyon üstü zipline ve öğle yemekli macera günü.",
                DistanceMeters = 55000, WalkingTimeMinutes = 600, Latitude = 37.1950, Longitude = 31.1820,
                Address = "Köprülü Kanyon Milli Parkı, Manavgat, Antalya", Phone = "+90 242 746 11 22", PriceLevel = 2, Rating = 4.9m, ReviewCount = 1350,
                WorkingHours = "08:30 - 17:30", HotelDiscountNote = "Otel Kapısından VIP Klimalı Transfer Dahil & %20 İndirim",
                HighlightFeatures = "14 km Rafting Parkuru, 800m Zipline, Nehir Kenarı Barbekü Öğle Yemeği",
                Specialties = "Buz Gibi Kaynak Suyunda Yüzme, Tarihi Roma Köprüsü Altında Mola",
                ImageUrl = "/images/boat_tour.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Koprulu+Kanyon+Rafting", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = antHotel.Id,
                Name = "Olympos Teleferik & Tahtalı Dağı Zirve Seyri (2365m)",
                Category = "Activity",
                SubCategory = "Dağ Zirvesi Teleferik & Panoramik Seyir",
                Description = "Deniz seviyesinden 10 dakikada 2365 metre yüksekliğindeki Tahtalı Dağı zirvesine tırmanan Avrupa'nın en uzun teleferik deneyimi.",
                DistanceMeters = 65000, WalkingTimeMinutes = 750, Latitude = 36.5360, Longitude = 30.4340,
                Address = "Tekirova Kemer, Antalya", Phone = "+90 242 242 22 55", PriceLevel = 3, Rating = 5.0m, ReviewCount = 1890,
                WorkingHours = "09:00 - 18:00", HotelDiscountNote = "Otel Misafirlerine %15 Teleferik Bileti İndirimi & Zirvede Sıcak İçecek",
                HighlightFeatures = "4350 Metre Teleferik Hattı, 360 Derece Akdeniz ve Toros Zirve Manzarası",
                Specialties = "Bulutların Üzerinde Seyir Terası, Zirvede Yamaç Paraşütü İmkanı",
                ImageUrl = "/images/boat_tour.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Olympos+Teleferik", IsPartner = true
            });

            // Antalya Seçkin Restoranlar & Barlar
            TryAdd(new NearbyPlace
            {
                HotelId = antHotel.Id,
                Name = "Lara Falezler Sunset Lounge & Cocktail Bar",
                Category = "Bar",
                SubCategory = "Falez Manzaralı Gün Batımı Barı",
                Description = "Lara falezlerinin en uç noktasında denize 40 metre yükseklikten gün batımı, canlı caz melodileri ve imza kokteyller.",
                DistanceMeters = 1200, WalkingTimeMinutes = 15, Latitude = 36.8520, Longitude = 30.8200,
                Address = "Şirinyalı Mah. Lara Cad. No:92, Muratpaşa", Phone = "+90 242 316 40 40", PriceLevel = 3, Rating = 4.8m, ReviewCount = 480,
                WorkingHours = "16:00 - 02:00", HotelDiscountNote = "Otel Misafirlerine %15 İndirim & Gün Batımı Rezervasyon Önceliği",
                HighlightFeatures = "Falezden Akdeniz Panoraması, Miksoloji Kokteyller, Canlı Saksafon Dinletisi",
                Specialties = "Mediterranean Sunset Kokteyli, Çıtır Kalamar Tabağı",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Lara+Falezler+Bar", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = antHotel.Id,
                Name = "Seraser Fine Dining Restaurant",
                Category = "Restaurant",
                SubCategory = "Tarihi Konak & Akdeniz Fine Dining",
                Description = "300 yıllık tarihi Kaleiçi konağının narenciye bahçesinde piyano ezgileri eşliğinde Akdeniz lezzetleri.",
                DistanceMeters = 8500, WalkingTimeMinutes = 100, Latitude = 36.8845, Longitude = 30.7055,
                Address = "Kaleiçi, Karanlık Sok. No:18, Muratpaşa, Antalya", Phone = "+90 242 248 70 18", PriceLevel = 4, Rating = 4.9m, ReviewCount = 890,
                WorkingHours = "18:00 - 23:30", HotelDiscountNote = "Otel Misafirlerine Şefin Özel Tatlısı & VIP Şarap İkramı",
                HighlightFeatures = "Tarihi Narenciye Avlusu, Canlı Piyano, Gurme Şarap Kavi",
                Specialties = "Kuzu İncik Konfi, Deniz Mahsullü Ev Yapımı Tagliolini, Hurmalı Sufle",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Seraser+Antalya", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = antHotel.Id,
                Name = "7 Mehmet Akdeniz & Türk Gastronomisi",
                Category = "Restaurant",
                SubCategory = "Ödüllü Türk & Antalya Mutfağı",
                Description = "Antalya'nın 80 yıllık efsane lezzet durağı; yerel narenciye, oğlak eti ve deniz mahsulleriyle hazırlanan gastronomi şaheserleri.",
                DistanceMeters = 9500, WalkingTimeMinutes = 110, Latitude = 36.8790, Longitude = 30.6650,
                Address = "Meltem Mah. Atatürk Kültür Parkı İçi, Muratpaşa", Phone = "+90 242 238 52 00", PriceLevel = 4, Rating = 5.0m, ReviewCount = 1650,
                WorkingHours = "11:00 - 23:00", HotelDiscountNote = "Otel Misafirlerine Özel Masa Rezervasyonu & Meze Tadım Tabağı",
                HighlightFeatures = "Dünyaca Ünlü Türk Mutfağı, Atatürk Parkı Deniz Manzarası, Geniş Meze Çeşidi",
                Specialties = "Tahinli Kabak Tatlısı, Kömürde Fırınlanmış Oğlak Tandır, Avokadolu Karides",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=7+Mehmet+Antalya", IsPartner = true
            });
        }

        // ─── 4. KAPADOKYA AKTİVİTELERİ & MEKANLARI (Cave Palace) ───
        if (kapHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = kapHotel.Id,
                Name = "Kapadokya Gün Doğumu Sıcak Hava Balon Turu",
                Category = "Activity",
                SubCategory = "Sıcak Hava Balonu & Havadan Seyir",
                Description = "Güneşin ilk ışıklarıyla peribacaları, Aşk Vadisi ve Göreme vadileri üzerinde süzülen masalsı sıcak hava balon uçuşu.",
                DistanceMeters = 800, WalkingTimeMinutes = 10, Latitude = 38.6480, Longitude = 34.8320,
                Address = "Göreme Balon Kalkış Alanı, Göreme, Kapadokya", Phone = "+90 384 271 30 00", PriceLevel = 4, Rating = 5.0m, ReviewCount = 2800,
                WorkingHours = "05:00 - 08:30", HotelDiscountNote = "Otelden Erken Rezervasyonda %15 VIP İndirim & Şampanya Kutlaması",
                HighlightFeatures = "60 Dakika Havada Kalış, Uçuş Sertifikası, Şampanya Kutlaması, Otelden Transfer",
                Specialties = "Gündoğumu Peribacaları Manzarası, 1000 Metre İrtifa Seyri",
                ImageUrl = "/images/cappadocia_balloon.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Goreme+Balon+Kalkis", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = kapHotel.Id,
                Name = "Aşk Vadisi & Kılıçlar Vadisi Gün Batımı ATV Turu",
                Category = "Activity",
                SubCategory = "Off-Road Safari & Vadi Keşfi",
                Description = "Peribacalarının arasından geçen vadilerde rehber eşliğinde heyecan dolu 2 saatlik gün batımı ATV safarisi.",
                DistanceMeters = 1200, WalkingTimeMinutes = 15, Latitude = 38.6600, Longitude = 34.8210,
                Address = "Aşk Vadisi Yolu No:3, Göreme, Kapadokya", Phone = "+90 384 271 14 14", PriceLevel = 2, Rating = 4.8m, ReviewCount = 740,
                WorkingHours = "10:00 - 19:30", HotelDiscountNote = "Otel Misafirlerine Kask, Toz Maskesi & %20 Tur İndirimi",
                HighlightFeatures = "2 Saatlik Rehberli Parkur, Çavuşin Köy Ziyareti, Kılıçlar Vadisi Gün Batımı",
                Specialties = "Kapadokya Vadilerinde Adrenalin Sürüşü, Panoramik Tepe Molası",
                ImageUrl = "/images/atv_safari.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Goreme+ATV+Safari", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = kapHotel.Id,
                Name = "Peribacaları Arasında Gün Batımı Atlı Safari",
                Category = "Activity",
                SubCategory = "Atlı Safari & Doğa Turu",
                Description = "Antik adı 'Güzel Atlar Ülkesi' olan Kapadokya'nın gizli kanyonlarında uysal Anadolu atlarıyla gün batımında büyüleyici yürüyüş.",
                DistanceMeters = 1500, WalkingTimeMinutes = 18, Latitude = 38.6410, Longitude = 34.8450,
                Address = "Zemi Vadisi Girişi, Göreme, Kapadokya", Phone = "+90 384 271 28 88", PriceLevel = 3, Rating = 4.9m, ReviewCount = 590,
                WorkingHours = "16:00 - 20:00", HotelDiscountNote = "Otel Misafirlerine Özel Kovboy Şapkası & Profesyonel Fotoğraf Çekimi",
                HighlightFeatures = "Güllüdere Vadisi Rotası, Deneyimli Seyisler, Yeni Başlayanlara Uygun Atlar",
                Specialties = "Gün Batımı Peribacaları Arasında At Üstünde Şarap Molası",
                ImageUrl = "/images/cappadocia_balloon.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Goreme+At+Ciftligi", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = kapHotel.Id,
                Name = "Avanos Geleneksel Çömlek Yapımı & Seramik Atölyesi",
                Category = "Activity",
                SubCategory = "Geleneksel Sanat & El Sanatları",
                Description = "Kızılırmak'ın kırmızı toprağıyla Hititler'den beri süregelen çömlek çarkı başına geçerek kendi vazonuzu yapma deneyimi.",
                DistanceMeters = 8200, WalkingTimeMinutes = 95, Latitude = 38.7180, Longitude = 34.8460,
                Address = "Kızılırmak Kenarı, Orta Mah., Avanos, Nevşehir", Phone = "+90 384 511 40 40", PriceLevel = 1, Rating = 4.9m, ReviewCount = 820,
                WorkingHours = "09:00 - 19:00", HotelDiscountNote = "Otel Misafirlerine Ücretsiz Çömlek Yapım Dersi & Ürün Alımlarında %20 İndirim",
                HighlightFeatures = "Usta Çömlekçiler Eşliğinde Çark Eğitimi, Tarihi Kaya Oyma Atölye, Ücretsiz Çay/Kahve",
                Specialties = "Yaptığınız Çömleği Hatıra Olarak Yanınıza Alma Fırsatı",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Chez+Galip+Avanos", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = kapHotel.Id,
                Name = "Derinkuyu Yeraltı Şehri & Gizemli Tüneller Gezisi",
                Category = "Activity",
                SubCategory = "Yeraltı Şehri & Tarih Turu",
                Description = "Yer altına 8 kat inen, 20.000 kişinin sığınabileceği havalandırma bacaları, kiliseleri ve şaraphaneleriyle akıl almaz yeraltı kenti.",
                DistanceMeters = 28000, WalkingTimeMinutes = 340, Latitude = 38.3735, Longitude = 34.7345,
                Address = "Derinkuyu İlçe Merkezi, Nevşehir", Phone = "+90 384 381 21 00", PriceLevel = 2, Rating = 4.9m, ReviewCount = 1420,
                WorkingHours = "08:00 - 19:00", HotelDiscountNote = "Otelden Rehberli Transfer Turunda %15 İndirim",
                HighlightFeatures = "8 Katlı Yeraltı Kompleksi, Havalandırma Bacası, Misyoner Okulu, Kuyu Sistemi",
                Specialties = "Kaya Oyma Labirent Tünellerde Profesyonel Tarihçi Rehberliği",
                ImageUrl = "/images/historical_monument.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Derinkuyu+Yeralti+Sehri", IsPartner = true
            });

            // Kapadokya Seçkin Restoranlar & Barlar
            TryAdd(new NearbyPlace
            {
                HotelId = kapHotel.Id,
                Name = "Dibek Tarihi Testi Kebabı Konağı",
                Category = "Restaurant",
                SubCategory = "Kapadokya Geleneksel & Kaya Konak",
                Description = "475 yıllık tarihi taş konakta yer sofrasında odun ateşinde pişirilen orijinal Kapadokya testi kebabı kırılma seremonisi.",
                DistanceMeters = 300, WalkingTimeMinutes = 4, Latitude = 38.6435, Longitude = 34.8295,
                Address = "Belediye Cad. Hakki Pasa Meydanı, Göreme", Phone = "+90 384 271 22 09", PriceLevel = 3, Rating = 4.9m, ReviewCount = 1250,
                WorkingHours = "12:00 - 22:30", HotelDiscountNote = "Otel Misafirlerine Özel Testi Kırma Seremonisi & Ev Yapımı Baklava İkramı",
                HighlightFeatures = "475 Yıllık Tarihi Taş Konak, Geleneksel Yer Sofrası, Odun Fırınında Pişen Testi Kebabı",
                Specialties = "Dana Etli Testi Kebabı, Kuru Patlıcan Dolması, Köy Yoğurdu",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Dibek+Goreme", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = kapHotel.Id,
                Name = "Red Valley Sunset Wine & Tapas Terrace",
                Category = "Bar",
                SubCategory = "Kızılçukur Vadisi Şarap & Gün Batımı Barı",
                Description = "Kapadokya'nın en ünlü gün batımı noktasında tüf kayalara karşı yerel Öküzgözü/Boğazkere şarapları ve tapaslar.",
                DistanceMeters = 1800, WalkingTimeMinutes = 22, Latitude = 38.6520, Longitude = 34.8550,
                Address = "Kızılçukur Vadisi Seyir Tepesi No:1, Göreme", Phone = "+90 384 271 90 90", PriceLevel = 2, Rating = 4.8m, ReviewCount = 670,
                WorkingHours = "16:00 - 22:00", HotelDiscountNote = "Otel Misafirlerine Peynir Tabağı Eşliğinde İlk Kadeh Şarap İkramı",
                HighlightFeatures = "Kızıl Vadide Masalsı Gün Batımı, Yerel Kapadokya Şarapları, Canlı Akustik Gitar",
                Specialties = "Emir Beyaz Şarabı, İsli Peynir Tabağı, Sıcak Şarap (Kışın)",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Kizilcukur+Sunset+Point", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = kapHotel.Id,
                Name = "Seki Restaurant & Argos Mahzen",
                Category = "Restaurant",
                SubCategory = "Kaya Oyma Fine Dining & Tarihi Mahzen",
                Description = "Güvercinlik Vadisi'ne bakan yamaçta binlerce yıllık kaya oyma manastır mahzeninde modern Anadolu gurme lezzetleri.",
                DistanceMeters = 3200, WalkingTimeMinutes = 38, Latitude = 38.6295, Longitude = 34.8050,
                Address = "Kayabaşı Cad. No:23, Uçhisar, Kapadokya", Phone = "+90 384 219 27 27", PriceLevel = 4, Rating = 4.9m, ReviewCount = 780,
                WorkingHours = "12:00 - 23:00", HotelDiscountNote = "Otel Misafirlerine Tarihi Yeraltı Şarap Mahzeni Özel Turu & %15 İndirim",
                HighlightFeatures = "Güvercinlik Vadisi Manzarası, Binlerce Yıllık Tarihi Mahzen, Michelin Rehberi Tavsiyeli",
                Specialties = "Ağır Ateşte Pişmiş Kuzu Gerdan, Firik Pilavı, Yaban Mersinli Panna Cotta",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Seki+Restaurant+Uchisar", IsPartner = true
            });
        }

        // ─── 5. ÇEŞME / ALAÇATI AKTİVİTELERİ & MEKANLARI (Alaçatı Windmill) ───
        if (cesHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = cesHotel.Id,
                Name = "Alaçatı Sörf Koyu Rüzgar Sörfü & Kitesurf",
                Category = "Activity",
                SubCategory = "Sörf Okulu & Ekipman Kiralama",
                Description = "Sabit rüzgarı ve sığ deniziyle dünyanın en iyi sörf koylarından birinde profesyonel eğitmenlerle windsurf ve kitesurf.",
                DistanceMeters = 3500, WalkingTimeMinutes = 42, Latitude = 38.2510, Longitude = 26.3920,
                Address = "Alaçatı Sörf Mevkii No:44, Çeşme, İzmir", Phone = "+90 232 716 67 77", PriceLevel = 3, Rating = 4.9m, ReviewCount = 590,
                WorkingHours = "09:00 - 18:30", HotelDiscountNote = "Otelden Ücretsiz Plaj Servisi + Sörf Derslerinde %20 İndirim",
                HighlightFeatures = "VDWS Sertifikalı Eğitmenler, Son Model Ekipmanlar, SUP Kiralama",
                Specialties = "2 Saatlik Başlangıç Paketi, Gün Batımı SUP Kürek Sörfü",
                ImageUrl = "/images/windsurf_cesme.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Alacati+Surf+Club", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = cesHotel.Id,
                Name = "Delikli Koy & Beyaz Kayalıklar SUP Kürek Sörfü Keşfi",
                Category = "Activity",
                SubCategory = "Doğa Harikası Koy & SUP Safari",
                Description = "Kireçtaşı beyaz falezleri ve dalgasız berrak sularıyla ünlü Delikli Koy'da sabah erken saatlerde stand-up paddle (SUP) safari.",
                DistanceMeters = 4500, WalkingTimeMinutes = 55, Latitude = 38.2410, Longitude = 26.3520,
                Address = "Delikli Koy Mevkii, Alaçatı, Çeşme", Phone = "+90 232 716 99 22", PriceLevel = 2, Rating = 4.8m, ReviewCount = 490,
                WorkingHours = "08:00 - 18:00", HotelDiscountNote = "Otel Misafirlerine SUP Board Kiralama %20 İndirimli & Su Geçirmez Çanta Hediyesi",
                HighlightFeatures = "Beyaz Doğal Kaya Kemerleri, Kalabalıktan Uzak Sakin Sabah Denizi, Drone Video Çekimi",
                Specialties = "Doğal Kaya Mağaraları Arasında Kürek Rotası",
                ImageUrl = "/images/windsurf_cesme.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Delikli+Koy+Alacati", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = cesHotel.Id,
                Name = "Çeşme Ilıca Termal & Eşek Adası Tekne Turu",
                Category = "Activity",
                SubCategory = "Mavi Yolculuk & Doğal Akvaryum Koyu",
                Description = "Çeşme Marina'dan kalkan lüks ahşap teknelerle Mavi Koy, Eşek Adası ve denizden kaynayan sıcak su Ilıca koylarında yüzme turu.",
                DistanceMeters = 6500, WalkingTimeMinutes = 75, Latitude = 38.3180, Longitude = 26.3550,
                Address = "Çeşme Belediye Yat Limanı, Çeşme, İzmir", Phone = "+90 232 712 11 00", PriceLevel = 2, Rating = 4.9m, ReviewCount = 740,
                WorkingHours = "10:00 - 17:30", HotelDiscountNote = "Otel Misafirlerine %15 İndirim & Teknede Öğle Balık Menüsü Dahil",
                HighlightFeatures = "Eşek Adası Sevimli Sakinleriyle Buluşma, Turkuaz Mavi Koy Dalış Molası, Şnorkel",
                Specialties = "Taze Çipura Izgara, Sakızlı Kurabiye ve Çay İkramı",
                ImageUrl = "/images/boat_tour.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Cesme+Tekne+Turlari", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = cesHotel.Id,
                Name = "Urla Bağ Yolu & Şarap Tadım Gastronomi Gezisi",
                Category = "Activity",
                SubCategory = "Bağ Bozumu & Gurme Şarap Turu",
                Description = "Urla'nın ödüllü butik bağlarında sommelier rehberliğinde Urla Karası ve Bornova Misketi şaraplarının tadımı ve gurme öğle yemeği.",
                DistanceMeters = 38000, WalkingTimeMinutes = 450, Latitude = 38.3220, Longitude = 26.7640,
                Address = "Kuşçular Köyü Bağ Yolu, Urla, İzmir", Phone = "+90 232 759 01 11", PriceLevel = 4, Rating = 5.0m, ReviewCount = 680,
                WorkingHours = "11:00 - 19:00", HotelDiscountNote = "Otel Misafirlerine Özel VIP Mercedes Vito Ulaşım & Şarap Şişelerinde %15 İndirim",
                HighlightFeatures = "3 Farklı Butik Bağ Ziyareti, Şarap Mahzenleri Turu, Peynir ve Şarküteri Eşleşmesi",
                Specialties = "Urla Karası ve Rezerve Cabernet Tadımı, Zeytinyağı Tadım Atölyesi",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Urla+Sarap+Tadimi", IsPartner = true
            });

            // Çeşme Seçkin Restoranlar & Barlar
            TryAdd(new NearbyPlace
            {
                HotelId = cesHotel.Id,
                Name = "Asma Yaprağı Ege Mutfağı Alaçatı",
                Category = "Restaurant",
                SubCategory = "Ege Zeytinyağlıları & Butik Avlu",
                Description = "Alaçatı'nın asma yapraklarıyla gölgelenmiş tarihi avlusunda anne eli değmiş sıcak tencere yemekleri ve meşhur zeytinyağlılar.",
                DistanceMeters = 400, WalkingTimeMinutes = 5, Latitude = 38.2825, Longitude = 26.3760,
                Address = "Alaçatı Mah. 1005 Sok. No:50, Çeşme, İzmir", Phone = "+90 232 716 01 78", PriceLevel = 3, Rating = 4.9m, ReviewCount = 1450,
                WorkingHours = "18:00 - 00:00", HotelDiscountNote = "Otel Misafirlerine Rezervasyon Önceliği & Sakızlı Fırın Sütlaç İkramı",
                HighlightFeatures = "Mutfaktan Tencere Seçme Usulü, Sakin Taş Konak Bahçesi, Günlük Yerel Ot Mezesi",
                Specialties = "Fırında Kuzu Gerdan, Tarçınlı Köfte, Çalkama, Enginar Dolması",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Asma+Yapragi+Alacati", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = cesHotel.Id,
                Name = "Alaçatı Port Sunset Lounge & Cocktail Bar",
                Category = "Bar",
                SubCategory = "Marina Sunset Kokteyl Bar & Müzik",
                Description = "Alaçatı Port marinasında lüks yatların arasında gün batımı kokteylleri ve lounge DJ performansları.",
                DistanceMeters = 2800, WalkingTimeMinutes = 35, Latitude = 38.2530, Longitude = 26.3890,
                Address = "Port Alaçatı Marina İçi No:12, Çeşme, İzmir", Phone = "+90 232 716 98 00", PriceLevel = 3, Rating = 4.8m, ReviewCount = 610,
                WorkingHours = "17:00 - 02:00", HotelDiscountNote = "Otel Misafirlerine %15 İndirim & Marina Manzaralı VIP Loca Önceliği",
                HighlightFeatures = "Marina Yat Manzarası, İmza Sakızlı Kokteyller, Gün Batımı DJ Seti",
                Specialties = "Mastic Spritz, Ege Mandalineli Gin Tonic, Trüflü Karides",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Port+Alacati+Marina", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = cesHotel.Id,
                Name = "Ferdi Baba Balık Restoranı Port Marina",
                Category = "Restaurant",
                SubCategory = "Ege Balık & Gurme Meze",
                Description = "Çeşme'nin en ünlü balıkçısı; günlük taze Ege deniz ürünleri, fener kavurma ve leziz sıcak mezeler.",
                DistanceMeters = 2700, WalkingTimeMinutes = 33, Latitude = 38.2520, Longitude = 26.3880,
                Address = "Alaçatı Port Marina No:8, Çeşme, İzmir", Phone = "+90 232 716 90 01", PriceLevel = 4, Rating = 4.9m, ReviewCount = 980,
                WorkingHours = "12:00 - 00:00", HotelDiscountNote = "Otel Misafirlerine Özel Şef Karşılama İkramı & Tatlı Tabağı",
                HighlightFeatures = "Taze Günlük Balık Vitrini, Deniz Kenarı Masalar, Zengin Ege Meze Çeşitleri",
                Specialties = "Lagos Izgara, Deniz Mahsullü Börek, Ahtapot Izgara, Şevketi Bostan",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Ferdi+Baba+Port", IsPartner = true
            });
        }

        // ─── 6. ULUDAĞ / BURSA KAYAK & TERMAL AKTİVİTELERİ ───
        if (uluHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = uluHotel.Id,
                Name = "Uludağ 1. Gelişim Kayak & Snowboard Okulu",
                Category = "Activity",
                SubCategory = "Kış Sporları & Pist Rehberliği",
                Description = "Milli kayak antrenörleri eşliğinde başlangıç ve ileri düzey kayak/snowboard dersleri, skipass kartı ve son model kayak ekipman kiralama.",
                DistanceMeters = 150, WalkingTimeMinutes = 2, Latitude = 40.1040, Longitude = 29.1360,
                Address = "Uludağ 1. Gelişim Telesiyej İstasyonu, Bursa", Phone = "+90 224 285 22 11", PriceLevel = 3, Rating = 4.9m, ReviewCount = 890,
                WorkingHours = "08:30 - 17:00 (Kış Sezonu)", HotelDiscountNote = "Otel Misafirlerine %20 Skipass & Ekipman İndirimi",
                HighlightFeatures = "Özel ve Grup Dersleri, Telesiyej Hızlı Geçiş, Isıtmalı Kayak Odası",
                Specialties = "Fahri & Beceren Pistleri Rehberliği, Gece Kayak Turları",
                ImageUrl = "/images/uludag_ski.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Uludağ+Kayak+Pisti", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = uluHotel.Id,
                Name = "Uludağ Kar Motoru & Şömineli Zirve Safari",
                Category = "Activity",
                SubCategory = "Kar Motoru & Kış Safarisi",
                Description = "Çam ormanları arasında kar motorlarıyla Çobankaya ve Bakacak zirvesine heyecan dolu kış safarisi ve şömine başında sıcak şarap molası.",
                DistanceMeters = 800, WalkingTimeMinutes = 10, Latitude = 40.1100, Longitude = 29.1400,
                Address = "Çobankaya Mevkii, Uludağ Milli Parkı, Bursa", Phone = "+90 224 285 30 40", PriceLevel = 4, Rating = 4.9m, ReviewCount = 640,
                WorkingHours = "09:00 - 18:00", HotelDiscountNote = "Otel Misafirlerine %15 İndirim & Termal Eldiven Hediyesi",
                HighlightFeatures = "Bakacak Zirve Panoraması, Çift Kişilik Güçlü Kar Motorları, Profesyonel Rehber",
                Specialties = "Şöminede Sucuk Ekmek & Zencefilli Sıcak Şarap İkramı",
                ImageUrl = "/images/atv_safari.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Uludag+Kar+Motoru", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = uluHotel.Id,
                Name = "Çekirge & Oylat Tarihi Şifalı Termal Kaplıcalar",
                Category = "Activity",
                SubCategory = "Termal Spa, Hamam & Sağlık",
                Description = "Roma döneminden beri şifa dağıtan doğal kükürtlü sıcak su kaynakları, geleneksel Türk hamamı, kese-köpük ve termal kaplıca havuzları.",
                DistanceMeters = 24000, WalkingTimeMinutes = 280, Latitude = 40.2010, Longitude = 29.0250,
                Address = "Çekirge Cad. Tarihi Kaplıcalar Bölgesi, Osmangazi, Bursa", Phone = "+90 224 233 40 00", PriceLevel = 2, Rating = 4.8m, ReviewCount = 1120,
                WorkingHours = "07:00 - 23:00", HotelDiscountNote = "Otel Misafirlerine VIP Aile Odasında %20 Özel İndirim",
                HighlightFeatures = "42°C Doğal Mineralli Termal Su, Tarihi Kubbeli Türk Hamamı, Medikal Masaj",
                Specialties = "Geleneksel Kese Köpük Masajı ve Aromatik Bitki Çayları",
                ImageUrl = "/images/uludag_ski.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Bursa+Tarihi+Termal+Kaplica", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = uluHotel.Id,
                Name = "Zirve Şömineli Dağ Evi & İsviçre Fondü",
                Category = "Restaurant",
                SubCategory = "Alp Mutfağı & Gurme Fondü",
                Description = "Karlarla kaplı çam ağaçları ortasında açık şöminesi, taş mimarisi ve geleneksel peynir/et fondü sunumlarıyla unutulmaz bir kış ziyafeti.",
                DistanceMeters = 300, WalkingTimeMinutes = 4, Latitude = 40.1050, Longitude = 29.1370,
                Address = "Oteller Bölgesi Zirve Yolu No:5, Uludağ", Phone = "+90 224 285 24 50", PriceLevel = 4, Rating = 4.9m, ReviewCount = 760,
                WorkingHours = "12:00 - 00:00", HotelDiscountNote = "Otel Misafirlerine Özel Şömine Başı Rezervasyon Önceliği",
                HighlightFeatures = "Doğal Odun Şöminesi, Karlı Orman Manzarası, İsviçre Gravyer Peynirleri",
                Specialties = "Gravyer & Emmental Karışımı Peynir Fondü, Çikolatalı Dağ Çileği Fondüsü",
                ImageUrl = "/images/uludag_ski.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Zirve+Dagevi+Uludag", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = uluHotel.Id,
                Name = "Bursa Tarihi Kebapçı İskender (1867)",
                Category = "Restaurant",
                SubCategory = "Orijinal Bursa İskender Kebabı",
                Description = "1867'den günümüze İskender Efendi'nin mirası; bakır tabakta cızırdayan yayık tereyağı, tava yoğurdu ve tescilli et döner.",
                DistanceMeters = 28000, WalkingTimeMinutes = 320, Latitude = 40.1830, Longitude = 29.0620,
                Address = "Tarihi Ahşap Konak, Atatürk Cad. No:17, Osmangazi, Bursa", Phone = "+90 224 221 46 15", PriceLevel = 3, Rating = 4.9m, ReviewCount = 3850,
                WorkingHours = "11:30 - 21:00", HotelDiscountNote = "Otel Misafirlerine Şıra ve Kestane Şekeri İkramı",
                HighlightFeatures = "150 Yıllık Tescilli Reçete, Özel Yayık Köy Tereyağı, Ev Yapımı Kuru Üzüm Şırası",
                Specialties = "Hakiki Bursa İskender Kebabı, Fırın Sütlaç, Kestane Şekeri",
                ImageUrl = "/images/historical_monument.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Kebapci+Iskender+Bursa", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = uluHotel.Id,
                Name = "Sky Peak Aprés-Ski Bar & Sıcak Şarap Lounge",
                Category = "Bar",
                SubCategory = "Aprés-Ski & Akşam Eğlencesi",
                Description = "Kayak sonrası açık hava ateş çanakları etrafında gün batımı DJ performansları, baharatlı sıcak şarap ve enerjik kış eğlencesi.",
                DistanceMeters = 200, WalkingTimeMinutes = 3, Latitude = 40.1035, Longitude = 29.1355,
                Address = "1. Bölge Pist Çıkışı Meydanı, Uludağ", Phone = "+90 224 285 28 88", PriceLevel = 3, Rating = 4.8m, ReviewCount = 590,
                WorkingHours = "15:00 - 02:00", HotelDiscountNote = "Otel Misafirlerine %15 İndirim & Sıcak Şarapta 1 Alana 1 Bedava",
                HighlightFeatures = "Açık Hava Ateş Çukurları, Canlı DJ Setleri, Isıtmalı Cam Teras",
                Specialties = "Uludağ İmzalı Tarçın & Karanfilli Sıcak Şarap, Smoked Bourbon Kokteylleri",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Sky+Peak+Uludag", IsPartner = true
            });
        }

        // ─── 7. FETHİYE / ÖLÜDENİZ LAGÜN & DOĞA AKTİVİTELERİ ───
        if (fetHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = fetHotel.Id,
                Name = "Babadağ 1960m Tandem Yamaç Paraşütü (Skywalkers)",
                Category = "Activity",
                SubCategory = "Gökyüzü & Adrenalin Sporları",
                Description = "Dünyanın en iyi yamaç paraşütü noktası Babadağ zirvesinden Ölüdeniz turkuaz lagününe doğru 45 dakikalık nefes kesici süzülüş ve 4K video kaydı.",
                DistanceMeters = 450, WalkingTimeMinutes = 6, Latitude = 36.5320, Longitude = 29.1850,
                Address = "Ölüdeniz İniş Alanı Ofisi, Belcekız Plajı, Fethiye", Phone = "+90 252 617 01 22", PriceLevel = 4, Rating = 5.0m, ReviewCount = 2400,
                WorkingHours = "07:30 - 18:30", HotelDiscountNote = "Otel Misafirlerine %20 İndirim & Ücretsiz 4K GoPro Video/Fotoğraf Paketi",
                HighlightFeatures = "1960 Metre Zirve Çıkışı, Lisanslı Pilotlar, Ölüdeniz Lagünü Üzerinde Akrobasi",
                Specialties = "Gündoğumu ve Gün Batımı Uçuş Seansları, Termal Hava Akımı Uçuşu",
                ImageUrl = "/images/fethiye_lagoon.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Babadag+Yamac+Parasutu", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = fetHotel.Id,
                Name = "Kelebekler Vadisi & Kabak Koyu Özel Sürat Teknesi Turu",
                Category = "Activity",
                SubCategory = "Doğa & Kanyon Tekne Gezisi",
                Description = "Yalnızca denizden ulaşılabilen Kelebekler Vadisi şelalesi, Soğuk Su Kaynağı, Mavi Mağara ve Kabak Koyu turkuaz sularında yüzme turu.",
                DistanceMeters = 800, WalkingTimeMinutes = 10, Latitude = 36.5050, Longitude = 29.1270,
                Address = "Belcekız Sahil İskelesi, Ölüdeniz, Fethiye", Phone = "+90 252 617 04 40", PriceLevel = 3, Rating = 4.9m, ReviewCount = 1350,
                WorkingHours = "10:00 - 17:30", HotelDiscountNote = "Otel Misafirlerine %15 Özel İndirim & Taze Balık Menüsü Dahil",
                HighlightFeatures = "Kelebekler Vadisi Şelale Yürüyüşü, Kristal Mavi Mağara Dalışı, Şnorkel",
                Specialties = "Teknede Odun Ateşinde Tavuk/Levrek Izgara ve Akdeniz Mezeleri",
                ImageUrl = "/images/boat_tour.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Kelebekler+Vadisi+Tekne", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = fetHotel.Id,
                Name = "Ölüdeniz Mavi Lagün Kano & SUP Safari",
                Category = "Activity",
                SubCategory = "Durgun Su Sporları & Doğa",
                Description = "Dalgasız ve koruma altındaki Ölüdeniz Tabiat Parkı'nın sakin turkuaz lagününde şeffaf tabanlı kanolar ve kürek sörfü (SUP) keyfi.",
                DistanceMeters = 350, WalkingTimeMinutes = 4, Latitude = 36.5510, Longitude = 29.1190,
                Address = "Ölüdeniz Tabiat Parkı İçi, Fethiye", Phone = "+90 252 617 06 60", PriceLevel = 2, Rating = 4.8m, ReviewCount = 720,
                WorkingHours = "08:00 - 19:30", HotelDiscountNote = "Otel Misafirlerine İlk 1 Saat Kano Kiralaması Ücretsiz",
                HighlightFeatures = "Şeffaf Pleksiglas Tabanlı Kano, Deniz Kaplumbağalarını Gözlemleme, Sakin Sular",
                Specialties = "Sabah Erken Saatlerde Ayna Gibi Durgun Lagün Turu",
                ImageUrl = "/images/fethiye_lagoon.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Oludeniz+Kano+SUP", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = fetHotel.Id,
                Name = "Yacht Classic Mori Restoran & Gün Batımı",
                Category = "Restaurant",
                SubCategory = "Akdeniz Deniz Ürünleri & Gurme",
                Description = "Fethiye Körfezi üzerinde denize sıfır ahşap iskelede şefin özel Akdeniz deniz mahsulleri ve romantik gün batımı ziyafeti.",
                DistanceMeters = 12500, WalkingTimeMinutes = 150, Latitude = 36.6270, Longitude = 29.1020,
                Address = "1. Karagözler Mah. Fevzi Çakmak Cad. No:24, Fethiye", Phone = "+90 252 612 50 67", PriceLevel = 4, Rating = 4.9m, ReviewCount = 1180,
                WorkingHours = "12:00 - 00:00", HotelDiscountNote = "Otel Misafirlerine İskele Kenarı Öncelikli Rezervasyon & Prosecco İkramı",
                HighlightFeatures = "Deniz Üzeri Romantik İskele Masaları, Canlı Keman Dinletisi, Zengin Şarap Kavı",
                Specialties = "Tuzda Levrek, Deniz Mahsullü Risotto, Fırında Ahtapot Kolu",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Mori+Restaurant+Fethiye", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = fetHotel.Id,
                Name = "Buzz Beach Bar & Sunset Cocktail Lounge",
                Category = "Bar",
                SubCategory = "Sahil Kokteyl Barı & Akşam Eğlencesi",
                Description = "Belcekız kumsalına sıfır terasında taze egzotik meyvelerle hazırlanan imza kokteyller, gün batımı müzikleri ve samimi tatil atmosferi.",
                DistanceMeters = 250, WalkingTimeMinutes = 3, Latitude = 36.5450, Longitude = 29.1230,
                Address = "Ölüdeniz Cad. Belcekız Sahili No:15, Fethiye", Phone = "+90 252 617 00 29", PriceLevel = 3, Rating = 4.8m, ReviewCount = 940,
                WorkingHours = "09:00 - 02:00", HotelDiscountNote = "Otel Misafirlerine %15 İndirim & Hoş Geldiniz Kokteyli",
                HighlightFeatures = "Plaja Sıfır Teras, Gün Batımında DJ Performansı, Geniş Kokteyl Menüsü",
                Specialties = "Frozen Passionfruit Margarita, Lycian Sunset Cocktail, Çıtır Kalamar",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Buzz+Beach+Bar+Oludeniz", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = fetHotel.Id,
                Name = "Mozaik Bahçe Fethiye Hatay Mutfağı",
                Category = "Restaurant",
                SubCategory = "Geleneksel Hatay & Akdeniz Mutfağı",
                Description = "Fethiye merkezde begonviller altındaki bahçede Hatay yöresinin tescilli mezeleri, tepsi kebabı ve sıcak künefesi.",
                DistanceMeters = 11800, WalkingTimeMinutes = 140, Latitude = 36.6230, Longitude = 29.1110,
                Address = "Cumhuriyet Mah. 91. Sok. No:2, Fethiye", Phone = "+90 252 614 46 53", PriceLevel = 2, Rating = 4.9m, ReviewCount = 1420,
                WorkingHours = "12:00 - 23:00", HotelDiscountNote = "Otel Misafirlerine Közlenmiş Künefe İkramı",
                HighlightFeatures = "Açık Hava Çiçekli Bahçe, Orijinal Hatay Baharatları, Odun Fırını",
                Specialties = "Hatay Tepsi Kebabı, Zahter Salatası, Humus ve Hatay Künefesi",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Mozaik+Bahce+Fethiye", IsPartner = true
            });
        }

        // ─── 8. MARDİN / ARTUKLU TARİH & MEZOPOTAMYA KÜLTÜRÜ ───
        if (marHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = marHotel.Id,
                Name = "Dara Antik Kenti & Su Sarnıçları Kültür Turu",
                Category = "Activity",
                SubCategory = "Arkeoloji & Tarih Keşfi",
                Description = "Doğu Roma İmparatorluğu'nun askeri garnizonu olan Dara; devasa kaya mezarları, zindanlar ve 'Yerebatan Sarnıcı' benzeri su yapılarıyla büyüleyici bir tarih yolculuğu.",
                DistanceMeters = 30000, WalkingTimeMinutes = 360, Latitude = 37.1780, Longitude = 40.9520,
                Address = "Oğuz Köyü, Dara Antik Kenti Mevkii, Nusaybin Yolu, Mardin", Phone = "+90 482 212 10 10", PriceLevel = 2, Rating = 5.0m, ReviewCount = 1680,
                WorkingHours = "08:30 - 18:00", HotelDiscountNote = "Otel Misafirlerine Özel Arkeolog Rehber & Klimalı VIP Transfer",
                HighlightFeatures = "Kaya İçi 3 Katlı Su Sarnıcı, Nekropol Alanı, Antik Köprü ve Agora",
                Specialties = "Köyde Odun Ateşinde Süryani Çöreği ve Menengiç Kahvesi İkramı",
                ImageUrl = "/images/historical_monument.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Dara+Antik+Kenti", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = marHotel.Id,
                Name = "Deyrulzafaran Manastırı & Güneş Tapınağı Turu",
                Category = "Activity",
                SubCategory = "Kültür, Tarih & İnanç Mirası",
                Description = "5. yüzyıldan günümüze Süryani Ortodoks Kilisesi'nin en önemli merkezlerinden biri. Güneş tapınağı temelleri üzerine inşa edilen taş mimari şaheseri.",
                DistanceMeters = 9500, WalkingTimeMinutes = 115, Latitude = 37.2990, Longitude = 40.7930,
                Address = "Eskikale Mah. Deyrulzafaran Yolu No:1, Artuklu, Mardin", Phone = "+90 482 213 14 25", PriceLevel = 2, Rating = 4.9m, ReviewCount = 1950,
                WorkingHours = "09:00 - 17:00", HotelDiscountNote = "Otel Misafirlerine Manastır Giriş Önceliği ve Safran Çayı İkramı",
                HighlightFeatures = "4500 Yıllık Güneş Tapınağı Temelleri, Tarihi İnciller, Kemerli Taş Avlular",
                Specialties = "Hakiki Mardin Safran Çayı ve Manastır Süryani Çöreği",
                ImageUrl = "/images/mardin_mansion.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Deyrulzafaran+Manastiri", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = marHotel.Id,
                Name = "Cercis Murat Konağı Geleneksel Mardin & Süryani Sofrası",
                Category = "Restaurant",
                SubCategory = "Tarihi Taş Konak & Gurme Gastronomi",
                Description = "1888 yapımı tarihi taş konakta Ebru Baybara Demir öncülüğünde yöre kadınlarının ellerinden çıkan masalsı Mardin ve Süryani lezzetleri.",
                DistanceMeters = 600, WalkingTimeMinutes = 8, Latitude = 37.3140, Longitude = 40.7410,
                Address = "1. Cad. No:517, Eski Mardin, Artuklu", Phone = "+90 482 213 68 41", PriceLevel = 4, Rating = 4.9m, ReviewCount = 2850,
                WorkingHours = "12:00 - 00:00", HotelDiscountNote = "Otel Misafirlerine Mezopotamya Manzaralı Masa Önceliği & Mahlepli Şarap Tadımı",
                HighlightFeatures = "130 Yıllık Taş Konak Avlusu, Geleneksel Reyhani Dansı Gösterisi, Kadın Kooperatifi Ürünleri",
                Specialties = "Kazan Kebabı, Vişneli Kuzu Gerdan, Tarçınlı Mahlepli Süryani Pilavı, Harire Tatlısı",
                ImageUrl = "/images/mardin_mansion.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Cercis+Murat+Konagi", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = marHotel.Id,
                Name = "Mezopotamya Teras Seyir & Butik Süryani Şarabı Evi",
                Category = "Bar",
                SubCategory = "Tarihi Teras Bar & Butik Şaraplar",
                Description = "Eski Mardin'in çatılarından sonsuz Mezopotamya ovasını seyrederek binlerce yıllık Süryani bağlarının butik şaraplarını ve yöresel peynirleri tadın.",
                DistanceMeters = 400, WalkingTimeMinutes = 5, Latitude = 37.3125, Longitude = 40.7390,
                Address = "Tarihi Çarşı Üstü Terası, Şar Mah., Artuklu, Mardin", Phone = "+90 482 212 90 45", PriceLevel = 3, Rating = 4.9m, ReviewCount = 870,
                WorkingHours = "16:00 - 01:00", HotelDiscountNote = "Otel Misafirlerine %15 İndirim & Süryani Peynir Tabağı İkramı",
                HighlightFeatures = "Panoramik Gün Batımı Mezopotamya Manzarası, Canlı Ney ve Ud Dinletisi, Taş Kemer Teras",
                Specialties = "Boğazkere & Öküzgözü Süryani Şarabı, Mahlepli Özel Şarap, İsli Mardin Peyniri",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Mezopotamya+Teras+Mardin", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = marHotel.Id,
                Name = "Bağdadi Tarihi Taş Restoran & Fasıl Geceleri",
                Category = "Restaurant",
                SubCategory = "Taş Konak Mutfağı & Fasıl Eğlencesi",
                Description = "Yüksek taş kemerleri, otantik sedirleri ve akşamları canlı fasıl dinletisi eşliğinde kaburga dolması ve tescilli Mardin mezeleri.",
                DistanceMeters = 500, WalkingTimeMinutes = 6, Latitude = 37.3135, Longitude = 40.7400,
                Address = "Vali Ozan Cad. No:22, Eski Mardin, Artuklu", Phone = "+90 482 212 89 60", PriceLevel = 3, Rating = 4.8m, ReviewCount = 1240,
                WorkingHours = "13:00 - 00:00", HotelDiscountNote = "Otel Misafirlerine Mırra ve Mardin Badem Şekeri İkramı",
                HighlightFeatures = "Canlı Klasik Türk Müziği & Fasıl, Tarihi Taş Kemer Mimarisi, Zengin Meze Çeşitleri",
                Specialties = "Mardin Usulü Kaburga Dolması, Sembusek, İrok (Mardin İçli Köftesi)",
                ImageUrl = "/images/mardin_mansion.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Bagdadi+Restoran+Mardin", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = marHotel.Id,
                Name = "Marangozlar Kahvesi & Tarihi Menengiç Molası",
                Category = "Bar",
                SubCategory = "Geleneksel Teras Kahvesi & Dinlenme",
                Description = "Tarihi marangozlar çarşısının üzerinde asılı duran ahşap balkonda ovaya karşı közde pişen sütlü menengiç kahvesi ve nargile keyfi.",
                DistanceMeters = 300, WalkingTimeMinutes = 4, Latitude = 37.3115, Longitude = 40.7385,
                Address = "Tarihi Marangozlar Çarşısı Üstü, Artuklu, Mardin", Phone = "+90 482 212 55 10", PriceLevel = 1, Rating = 4.9m, ReviewCount = 1530,
                WorkingHours = "08:00 - 23:30", HotelDiscountNote = "Otel Misafirlerine Yöresel Mırra ve Cevizli Sucuk İkramı",
                HighlightFeatures = "Uçurum Kenarı Seyir Balkonu, Kumda Pişirilen Kahveler, Otantik Ahşap Tabureler",
                Specialties = "Hakiki Sütlü Menengiç Kahvesi, Dibek Kahvesi, Geleneksel Çifte Kavrulmuş Badem",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Marangozlar+Kahvesi+Mardin", IsPartner = true
            });
        }

        // ─── 9. PARİS AKTİVİTELERİ & MEKANLARI ───
        if (parisHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = parisHotel.Id,
                Name = "Eyfel Kulesi & Seine Nehri Romantik Akşam Tekne Turu",
                Category = "Activity",
                SubCategory = "Nehir Gezisi & Şampanya",
                Description = "Işıklar şehri Paris'i Seine nehri üzerinden cam tavanlı lüks teknede keşfedin. Eyfel Kulesi, Notre Dame ve tarihi köprüler eşliğinde akşam şampanyası.",
                DistanceMeters = 2400, WalkingTimeMinutes = 25, Latitude = 48.8584, Longitude = 2.2945,
                Address = "Port de la Bourdonnais, 75007 Paris", Phone = "+33 1 76 64 14 45", PriceLevel = 3, Rating = 4.9m, ReviewCount = 820,
                WorkingHours = "10:00 - 23:00", HotelDiscountNote = "Otel Misafirlerine Özel VIP Loca & Fransız Şampanyası İkramı",
                HighlightFeatures = "Panoramik Cam Tavan, Eyfel Kulesi Gece Işıkları, Canlı Akordeon Dinletisi, Şampanya İkramı",
                Specialties = "Moët & Chandon Şampanya, Fransız Makaron Tabağı",
                ImageUrl = "/images/paris_palace.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Bateaux+Parisiens+Eiffel", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = parisHotel.Id,
                Name = "Louvre Müzesi & Mona Lisa Özel Rehberli Sanat Turu",
                Category = "Activity",
                SubCategory = "Tarih & Sanat Müzesi",
                Description = "Dünyanın en prestijli müzesinde sıra beklemeden Mona Lisa, Milo Venüsü ve Fransız krallık başyapıtlarını uzman sanat tarihçisiyle keşif.",
                DistanceMeters = 850, WalkingTimeMinutes = 10, Latitude = 48.8606, Longitude = 2.3376,
                Address = "Rue de Rivoli, 75001 Paris", Phone = "+33 1 40 20 53 17", PriceLevel = 2, Rating = 4.9m, ReviewCount = 2450,
                WorkingHours = "09:00 - 18:00 (Çar-Cum 21:00'e kadar)", HotelDiscountNote = "Sıra Beklemeden VIP Hızlı Giriş & Kulaklık Sistemi Dahil",
                HighlightFeatures = "Mona Lisa Özel Geçişi, Cam Piramit Altı Giriş, Fransız Kraliyet Taç Mücevherleri",
                Specialties = "Sanat Tarihçisi Eşliğinde 2 Saatlik Özel Rota",
                ImageUrl = "/images/historical_monument.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Louvre+Museum+Paris", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = parisHotel.Id,
                Name = "Le Jules Verne Michelin Yıldızlı Eyfel Restoranı",
                Category = "Restaurant",
                SubCategory = "Fransız Haute Cuisine & Fine Dining",
                Description = "Eyfel Kulesi'nin 2. katında, 125 metre yükseklikte şef Frédéric Anton yönetiminde modern Fransız mutfağının zirve lezzetleri.",
                DistanceMeters = 2500, WalkingTimeMinutes = 26, Latitude = 48.8583, Longitude = 2.2944,
                Address = "Avenue Gustave Eiffel, 2ème étage, 75007 Paris", Phone = "+33 1 83 77 34 34", PriceLevel = 4, Rating = 5.0m, ReviewCount = 1150,
                WorkingHours = "12:00 - 13:30 / 19:00 - 21:30", HotelDiscountNote = "Otel Concierge Üzerinden Öncelikli Rezervasyon & Şefin Karşılama İkramı",
                HighlightFeatures = "125 Metre Yükseklikte 360° Paris Panoraması, Michelin Yıldızı, Özel Asansörle Giriş",
                Specialties = "Yabani Levrek & Havyar Sosu, Fransız Trüflü Ördek Ciğeri, Altın Varaklı Çikolata Küresi",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Le+Jules+Verne+Paris", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = parisHotel.Id,
                Name = "Bar Hemingway (Ritz Paris) Efsanevi Kokteyl Barı",
                Category = "Bar",
                SubCategory = "Efsanevi Speakeasy & Kokteyl Salonu",
                Description = "Ernest Hemingway ve F. Scott Fitzgerald'ın buluşma noktası olan, deri koltukları ve nostaljik kütüphane atmosferiyle dünyanın en ünlü kokteyl barı.",
                DistanceMeters = 150, WalkingTimeMinutes = 2, Latitude = 48.8682, Longitude = 2.3290,
                Address = "15 Place Vendôme, 75001 Paris", Phone = "+33 1 43 16 30 30", PriceLevel = 4, Rating = 4.9m, ReviewCount = 680,
                WorkingHours = "17:30 - 01:00", HotelDiscountNote = "Otel Misafirlerine Özel İmza 'Serendipity' Kokteyli İkramı",
                HighlightFeatures = "Colin Field İmzalı Kokteyller, Orijinal Hemingway Eşyaları, Ahşap Panelli Nostaljik Salon",
                Specialties = "İmza 'Serendipity' Kokteyli (Calvados, Taze Nane & Şampanya), Ritz Bellini",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Bar+Hemingway+Ritz+Paris", IsPartner = true
            });
        }

        // ─── 10. LONDRA AKTİVİTELERİ & MEKANLARI ───
        if (londonHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = londonHotel.Id,
                Name = "Thames Nehri Özel Hızlı RIB Tekne & Akşam Şampanya Turu",
                Category = "Activity",
                SubCategory = "Sürat Teknesi & Nehir Macerası",
                Description = "London Eye'dan Tower Bridge'e Thames sularında sürat teknesiyle adrenalin dolu seyahat ve ardından şampanya eşliğinde gün batımı.",
                DistanceMeters = 600, WalkingTimeMinutes = 7, Latitude = 51.5033, Longitude = -0.1195,
                Address = "Boarding Pier, Victoria Embankment, London", Phone = "+44 20 7928 8933", PriceLevel = 3, Rating = 4.9m, ReviewCount = 740,
                WorkingHours = "10:00 - 19:30", HotelDiscountNote = "Otel Misafirlerine %15 İndirim & Thames Hatıra Fotoğrafı",
                HighlightFeatures = "Tower Bridge Altından Geçiş, 35 Knot Hız Deneyimi, Londra İkonları Panoraması",
                Specialties = "Özel VIP Şampanya Kadehi & İngiliz Çilekli İkram",
                ImageUrl = "/images/boat_tour.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Thames+RIB+Experience+London", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = londonHotel.Id,
                Name = "Tarihi Londra Kulesi & Kraliyet Mücevherleri VIP Giriş",
                Category = "Activity",
                SubCategory = "Kraliyet Tarihi & Şato Turu",
                Description = "1000 yıllık Norman kalesi, Yeoman Warder muhafızları ve paha biçilmez İngiliz Kraliyet Taç Mücevherleri koleksiyonu.",
                DistanceMeters = 3400, WalkingTimeMinutes = 40, Latitude = 51.5081, Longitude = -0.0759,
                Address = "Tower of London, London EC3N 4AB", Phone = "+44 20 3166 6000", PriceLevel = 2, Rating = 4.8m, ReviewCount = 3100,
                WorkingHours = "09:00 - 17:30", HotelDiscountNote = "Sırasız VIP Hızlı Giriş Kartı",
                HighlightFeatures = "Kullinan Elmasları, Beyaz Kule, Zırh Koleksiyonu, Beefeater Rehberliği",
                Specialties = "Kraliyet Taç Odası Özel Sabah Ziyareti",
                ImageUrl = "/images/historical_monument.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Tower+of+London", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = londonHotel.Id,
                Name = "Gordon Ramsay Restaurant Royal Hospital Road (3 Michelin)",
                Category = "Restaurant",
                SubCategory = "İngiliz & Fransız Modern Fine Dining",
                Description = "Ünlü şef Gordon Ramsay'nin 3 Michelin yıldızlı efsanevi restoranı. Kusursuz Fransız teknikleri ve modern İngiliz lezzet uyumu.",
                DistanceMeters = 2800, WalkingTimeMinutes = 32, Latitude = 51.4855, Longitude = -0.1614,
                Address = "68 Royal Hospital Rd, Chelsea, London SW3 4HP", Phone = "+44 20 7352 4441", PriceLevel = 4, Rating = 5.0m, ReviewCount = 890,
                WorkingHours = "12:00 - 14:15 / 18:30 - 21:15", HotelDiscountNote = "Otel Misafirlerine Masada Şefin İkramı Sommelier Şarap Tadımı",
                HighlightFeatures = "3 Michelin Yıldızı, Özel Prestij Tadım Menüsü, Zarif Chelsea Ambiyansı",
                Specialties = "Istakoz & Langoustine Ravioli, Kereviz ve Trüflü Dana Bonfile",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Restaurant+Gordon+Ramsay+Chelsea", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = londonHotel.Id,
                Name = "Aqua Shard Panoramik 31. Kat Sky Bar & Kokteyl",
                Category = "Bar",
                SubCategory = "Gökdelen Bar & Thames Panoraması",
                Description = "Avrupa'nın en ikonik gökdelenlerinden The Shard'ın 31. katında Londra siluetine karşı hazırlanan özel botanik kokteyller.",
                DistanceMeters = 2900, WalkingTimeMinutes = 34, Latitude = 51.5045, Longitude = -0.0865,
                Address = "Level 31, The Shard, 31 St Thomas St, London", Phone = "+44 20 3011 1256", PriceLevel = 3, Rating = 4.8m, ReviewCount = 1420,
                WorkingHours = "12:00 - 01:00", HotelDiscountNote = "Girişte Sıra Beklemeden Panoramik Masa Garantisi",
                HighlightFeatures = "31. Kattan Thames ve Tower Bridge Panoraması, Modern İngiliz Kokteylleri, Canlı Akşam Müziği",
                Specialties = "İmza 'Shard Botanical' Cin Kokteyli, Earl Grey Çaylı Martini",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Aqua+Shard+London", IsPartner = true
            });
        }

        // ─── 11. NEW YORK AKTİVİTELERİ & MEKANLARI ───
        if (nyHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = nyHotel.Id,
                Name = "Central Park Özel Fayton & Şampanyalı Piknik Turu",
                Category = "Activity",
                SubCategory = "Park Keşfi & Romantik Gezi",
                Description = "New York'un kalbinde, Central Park göletleri, Bethesda Çeşmesi ve asırlık çınar ağaçları arasında klasik beyaz fayton ve piknik sepeti turu.",
                DistanceMeters = 200, WalkingTimeMinutes = 2, Latitude = 40.7650, Longitude = -73.9730,
                Address = "59th St & 5th Ave, Central Park South, New York", Phone = "+1 212 555 0192", PriceLevel = 3, Rating = 4.9m, ReviewCount = 980,
                WorkingHours = "09:00 - 22:00", HotelDiscountNote = "Otel Misafirlerine %20 İndirim & Gurme Piknik Sepeti Dahil",
                HighlightFeatures = "Bethesda Terası, Bow Bridge, Çilek Tarlaları (Strawberry Fields), Şampanya İkramı",
                Specialties = "Gurme New York Peynir Tabağı & Çilekli Şampanya",
                ImageUrl = "/images/newyork_skyline.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Central+Park+Carriage+Rides", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = nyHotel.Id,
                Name = "Top of the Rock & Rockefeller Center 360° Seyir Terası",
                Category = "Activity",
                SubCategory = "Gökdelen Seyir Terası & Şehir Panoraması",
                Description = "Rockefeller Center'ın 70. katından hem Empire State Binası'na hem de tüm Central Park'a bakan New York'un en çarpıcı açık hava terası.",
                DistanceMeters = 950, WalkingTimeMinutes = 11, Latitude = 40.7587, Longitude = -73.9787,
                Address = "30 Rockefeller Plaza, New York, NY 10112", Phone = "+1 212 698 2000", PriceLevel = 2, Rating = 4.9m, ReviewCount = 4200,
                WorkingHours = "09:00 - 23:00", HotelDiscountNote = "Gün Batımı Saatinde VIP Express Geçiş Bileti",
                HighlightFeatures = "Empire State Binası Karşısı, Açık Hava 360° Cam Teras, Hızlı VIP Asansör",
                Specialties = "Gün Batımı Panoramik Fotoğraf Çekim Noktası",
                ImageUrl = "/images/historical_monument.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Top+of+the+Rock+NYC", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = nyHotel.Id,
                Name = "Le Bernardin 3 Michelin Yıldızlı Deniz Ürünleri Restoranı",
                Category = "Restaurant",
                SubCategory = "Fransız & Deniz Ürünleri Haute Cuisine",
                Description = "Şef Eric Ripert'in efsanevi restoranı; 30 yılı aşkın süredir New York'un en beğenilen ve Michelin 3 yıldıza sahip deniz ürünleri mabedi.",
                DistanceMeters = 800, WalkingTimeMinutes = 9, Latitude = 40.7615, Longitude = -73.9817,
                Address = "155 W 51st St, New York, NY 10019", Phone = "+1 212 554 1515", PriceLevel = 4, Rating = 5.0m, ReviewCount = 1600,
                WorkingHours = "12:00 - 14:00 / 17:15 - 22:30", HotelDiscountNote = "Otel Misafirlerine Özel VIP Rezervasyon & Şef Eric Ripert İmzalı Tatlı",
                HighlightFeatures = "Michelin 3 Yıldız, Dünyanın En İyi Deniz Ürünleri Menüsü, Kusursuz Amerikan Servisi",
                Specialties = "Ton Balığı Carpaccio & Kaz Ciğeri, Kırmızı Ebegümeci Soslu Istakoz Kuyruğu",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Le+Bernardin+NYC", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = nyHotel.Id,
                Name = "The Campbell Grand Central Tarihi Speakeasy Kokteyl Barı",
                Category = "Bar",
                SubCategory = "1920'ler Finans Kralı Salonu & Kokteyl Bar",
                Description = "1923 yılında finans kralı John W. Campbell'ın özel ofisi olarak kurulan, dev taş şöminesi ve kurşunlu vitray pencereleriyle gizli bir speakeasy.",
                DistanceMeters = 1400, WalkingTimeMinutes = 16, Latitude = 40.7527, Longitude = -73.9772,
                Address = "15 Vanderbilt Ave, Grand Central Terminal, NY", Phone = "+1 212 297 1781", PriceLevel = 3, Rating = 4.8m, ReviewCount = 790,
                WorkingHours = "16:00 - 01:00", HotelDiscountNote = "Otel Misafirlerine İlk Kokteylde %15 İndirim ve Özel Loca",
                HighlightFeatures = "13 Metrelik Ahşap Tavan, 1920'ler Taş Şöminesi, Orijinal Kasa ve Antika Mobilyalar",
                Specialties = "The Campbell Manhattan, Grand Central Bourbon Punch, Trüflü Patates Kızartması",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=The+Campbell+NYC", IsPartner = true
            });
        }

        // ─── 12. DUBAİ AKTİVİTELERİ & MEKANLARI ───
        if (dubaiHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = dubaiHotel.Id,
                Name = "Dubai Marina & Palm Jumeirah Özel Süperyat Turu",
                Category = "Activity",
                SubCategory = "Lüks Yat Turu & Körfez Gezisi",
                Description = "Arap Körfezi'nin masmavi sularında, Atlantis The Royal, Burj Al Arab ve Dubai Eye manzarası eşliğinde kaptanlı özel yat deneyimi.",
                DistanceMeters = 1200, WalkingTimeMinutes = 14, Latitude = 25.1320, Longitude = 55.1200,
                Address = "Palm Marina West Pier, Palm Jumeirah, Dubai", Phone = "+971 4 399 5555", PriceLevel = 3, Rating = 5.0m, ReviewCount = 670,
                WorkingHours = "09:00 - 20:00", HotelDiscountNote = "Otel Misafirlerine %20 İndirim & Kaptan İkramı Barbekü",
                HighlightFeatures = "Burj Al Arab Açıklarında Yüzme Molası, Güneşlenme Terası, Şef Barbekü Menüsü",
                Specialties = "Yatta Taze Izgara Körfez Karidesi, Taze Hindistan Cevizi Suyu",
                ImageUrl = "/images/dubai_resort.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Dubai+Marina+Yacht+Charter", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = dubaiHotel.Id,
                Name = "Kızıl Kum Tepeleri Lüks Çöl Safarisi & ATV Dune Bashing",
                Category = "Activity",
                SubCategory = "Çöl Safarisi & Bedevi Kampı",
                Description = "Kızıl kum tepelerinde 4x4 araçlarla heyecan dolu tepe tırmanışı, deveye binme, kum sörfü ve 5 yıldızlı Bedevi vahası akşam yemeği.",
                DistanceMeters = 42000, WalkingTimeMinutes = 45, Latitude = 24.8900, Longitude = 55.5600,
                Address = "Lahbab Red Dunes, Dubai Çöl Koruma Alanı", Phone = "+971 4 457 2000", PriceLevel = 3, Rating = 4.9m, ReviewCount = 1850,
                WorkingHours = "14:30 - 21:30", HotelDiscountNote = "Otelden Özel 4x4 VIP Transfer & Şahin Gösterisi Dahil",
                HighlightFeatures = "Kızıl Kum Tepeleri (Red Dunes), VIP Çöl Çadırı, Ateş Dansı & Tanoura Gösterisi",
                Specialties = "Geleneksel Kuzu Tandır, Kakuleli Arap Kahvesi ve Hurma İkramı",
                ImageUrl = "/images/atv_safari.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Dubai+Desert+Safari", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = dubaiHotel.Id,
                Name = "Zuma Dubai Çağdaş Japon Restoranı & Lounge",
                Category = "Restaurant",
                SubCategory = "İzakaia Tarzı Modern Japon Mutfağı",
                Description = "DIFC bölgesinde dünya jet-setinin uğrak noktası; robata ızgara, taze suşi ve dinamik lüks atmosferiyle Dubai'nin ödüllü gastronomi yıldızı.",
                DistanceMeters = 18000, WalkingTimeMinutes = 22, Latitude = 25.2070, Longitude = 55.2770,
                Address = "Gate Village 06, DIFC, Dubai", Phone = "+971 4 425 5660", PriceLevel = 4, Rating = 4.9m, ReviewCount = 2100,
                WorkingHours = "12:00 - 15:30 / 19:00 - 01:00", HotelDiscountNote = "Özel VIP Masa & Şefin İkramı Wagyu Başlangıç Tabağı",
                HighlightFeatures = "Robata Kömür Izgara, Özel Suşi Tezgâhı, İki Katlı Mimari ve DJ Lounge",
                Specialties = "Miso Soslu Siyah Morina Balığı (Black Cod), Trüflü Wagyu Dana Bonfile",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Zuma+Dubai", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = dubaiHotel.Id,
                Name = "Aura Skypool 360° Dünyanın En Yüksek Sonsuzluk Havuzu Barı",
                Category = "Bar",
                SubCategory = "Gökdelen Sonsuzluk Havuzu & Lounge",
                Description = "200 metre yükseklikte, Palm Jumeirah ve Dubai siluetini 360 derece kesintisiz gören dünyanın en yüksek sonsuzluk havuzunda gün batımı lounge'u.",
                DistanceMeters = 2400, WalkingTimeMinutes = 28, Latitude = 25.1120, Longitude = 55.1400,
                Address = "The Palm Tower 50th Floor, Palm Jumeirah, Dubai", Phone = "+971 4 566 2121", PriceLevel = 4, Rating = 5.0m, ReviewCount = 1350,
                WorkingHours = "10:00 - 23:00", HotelDiscountNote = "Şezlong ve Havuz Girişinde %15 Otel İndirimi",
                HighlightFeatures = "200 Metre Yükseklikte 360° Havuz, Palm Jumeirah Kuşbakışı, Canlı DJ Performansları",
                Specialties = "Aura Sunset Kokteyli, Pan-Asya Tapasları, Taze Karides Dim Sum",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Aura+Skypool+Dubai", IsPartner = true
            });
        }

        // ─── 13. TOKYO AKTİVİTELERİ & MEKANLARI ───
        if (tokyoHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = tokyoHotel.Id,
                Name = "Ginza & Tsukiji Otantik Omakase Suşi Deneyimi",
                Category = "Restaurant",
                SubCategory = "Usta Şef Omakase & Taze Balık Sanatı",
                Description = "Her sabah Toyosu pazarından bizzat seçilen mavi yüzgeçli orkinos, deniz kestanesi ve mevsim balıklarıyla 20 parçalık şef omakase yolculuğu.",
                DistanceMeters = 1300, WalkingTimeMinutes = 15, Latitude = 35.6710, Longitude = 139.7650,
                Address = "4-Chome Ginza, Chuo City, Tokyo 104-0061", Phone = "+81 3 3567 1122", PriceLevel = 4, Rating = 5.0m, ReviewCount = 920,
                WorkingHours = "12:00 - 14:00 / 18:00 - 21:30", HotelDiscountNote = "Otel Misafirlerine Özel Karşılama Sake İkramı & VIP Tezgâh",
                HighlightFeatures = "Hinoki Ağacı Suşi Tezgâhı, Doğrudan Şef Etkileşimi, Nadir Niigata Sake Seçkisi",
                Specialties = "Otoro (Yağlı Ton Balığı) Nigiri, Hokkaido Deniz Kestanesi (Uni), Şef İmzalı Tamago",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Ginza+Sushi+Omakase+Tokyo", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = tokyoHotel.Id,
                Name = "TeamLab Planets Tokyo Dijital Sanat & Işık Müzesi",
                Category = "Activity",
                SubCategory = "İnteraktif Dijital Sanat & Su Müzesi",
                Description = "Yalınayak su içinde yürüdüğünüz, sonsuz kristal aynalar ve dijital koi balıklarının vücudunuza temas ettiği büyüleyici sürreal sanat sergisi.",
                DistanceMeters = 4200, WalkingTimeMinutes = 45, Latitude = 35.6490, Longitude = 139.7890,
                Address = "6-Chome-1-16 Toyosu, Koto City, Tokyo", Phone = "+81 3 6204 2681", PriceLevel = 2, Rating = 4.9m, ReviewCount = 3800,
                WorkingHours = "09:00 - 22:00", HotelDiscountNote = "Önceden Rezerve Sırasız Hızlı Giriş",
                HighlightFeatures = "Sonsuz Kristal Evren (The Infinite Crystal Universe), Yüzen Çiçek Bahçesi, Suda Yürüyüş",
                Specialties = "Vegan Ramen UZU Bahçe Kafe Girişi",
                ImageUrl = "/images/historical_monument.jpg", GoogleMapsUrl = "https://maps.google.com/?q=teamLab+Planets+Tokyo", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = tokyoHotel.Id,
                Name = "New York Bar (Park Hyatt Tokyo 52. Kat) Caz & Kokteyl",
                Category = "Bar",
                SubCategory = "Efsanevi Caz Bar & Tokyo Gece Panoraması",
                Description = "Sofia Coppola'nın 'Lost in Translation' filminin çekildiği, Shinjuku ışıklarına bakan cam kulede canlı New York caz müziği ve Japon viskileri.",
                DistanceMeters = 5800, WalkingTimeMinutes = 50, Latitude = 35.6850, Longitude = 139.6910,
                Address = "3-7-1-2 Nishi-Shinjuku, Shinjuku City, Tokyo", Phone = "+81 3 5322 1234", PriceLevel = 4, Rating = 4.9m, ReviewCount = 1100,
                WorkingHours = "17:00 - 00:00", HotelDiscountNote = "Masa Ücreti Muafiyeti & Otel Misafirine İmzalı Kokteyl",
                HighlightFeatures = "52. Kat Shinjuku Işıkları Panoraması, Canlı Amerikan Caz Kuarteti, Nadir Yamazaki & Hibiki Viskileri",
                Specialties = "L.I.T. İmzalı Kokteyl (Japon Viskisi & Yeşil Çay), Hibiki 21 Yıllık Tadım Kadehi",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=New+York+Bar+Park+Hyatt+Tokyo", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = tokyoHotel.Id,
                Name = "Meiji Tapınağı & Harajuku Geleneksel Çay Seremonisi Turu",
                Category = "Activity",
                SubCategory = "Geleneksel Şinto Tapınağı & Matcha Seremonisi",
                Description = "Yüzyıllık orman içinde huzur dolu Meiji Jingu tapınağında ruhani yürüyüş ve geleneksel tatami odasında Matcha çay seremonisi deneyimi.",
                DistanceMeters = 4800, WalkingTimeMinutes = 50, Latitude = 35.6764, Longitude = 139.6993,
                Address = "1-1 Yoyogikamizonocho, Shibuya City, Tokyo", Phone = "+81 3 3379 5511", PriceLevel = 2, Rating = 4.9m, ReviewCount = 2900,
                WorkingHours = "08:30 - 17:00", HotelDiscountNote = "Hakiki Kimono Kiralama & Matcha Çayı Tadımı Dahil",
                HighlightFeatures = "Dev Torii Ahşap Kapıları, Şinto Ritüelleri, Sake Fıçıları Duvarı, Matcha Sunumu",
                Specialties = "Geleneksel Uji Matcha Çayı ve Mevsimlik Wagashi Tatlısı",
                ImageUrl = "/images/tokyo_palace.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Meiji+Shrine+Tokyo", IsPartner = true
            });
        }

        // ─── 14. ROMA AKTİVİTELERİ & MEKANLARI ───
        if (romeHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = romeHotel.Id,
                Name = "Gece Işıklarında Özel Kolezyum & Yeraltı Gladyatör Tünelleri",
                Category = "Activity",
                SubCategory = "Antik Roma & Özel Arkeolojik Gezi",
                Description = "Gündüz kalabalığı dağıldıktan sonra ay ışığında Kolezyum'un gladyatör tünelleri ve arena zeminine özel arkeolog rehber eşliğinde giriş.",
                DistanceMeters = 350, WalkingTimeMinutes = 4, Latitude = 41.8902, Longitude = 12.4922,
                Address = "Piazza del Colosseo, 1, 00184 Roma", Phone = "+39 06 3996 7700", PriceLevel = 3, Rating = 5.0m, ReviewCount = 1450,
                WorkingHours = "20:00 - 23:00", HotelDiscountNote = "Sırasız Gece Turu & Özel Arkeolog Rehber Eşliğinde",
                HighlightFeatures = "Gladyatör Yeraltı Zindanları (Hypogeum), Arena Zemini, Ay Işığında Tarih",
                Specialties = "Gladyatörlerin Hazırlık Odaları ve Asansör Mekanizmaları",
                ImageUrl = "/images/rome_palace.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Colosseum+Rome", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = romeHotel.Id,
                Name = "Trastevere Otantik Sokak Lezzetleri & Şarap Tadımı Gezisi",
                Category = "Activity",
                SubCategory = "İtalyan Gastronomi & Şarap Kültürü",
                Description = "Arnavut kaldırımlı sarmaşıklı sokaklarda yerel pazarlar, peynir dükkanları, fırınlar ve asırlık şarap mahzenlerinde tadım rotası.",
                DistanceMeters = 1600, WalkingTimeMinutes = 19, Latitude = 41.8890, Longitude = 12.4700,
                Address = "Piazza di Santa Maria in Trastevere, Roma", Phone = "+39 06 580 3797", PriceLevel = 2, Rating = 4.9m, ReviewCount = 1890,
                WorkingHours = "17:00 - 22:00", HotelDiscountNote = "Otel Misafirlerine 4 Çeşit İtalyan Şarabı ve Peynir Tadımı",
                HighlightFeatures = "Taze Pişmiş Suppli, 24 Aylık Parmigiano Reggiano, Chianti Şarap Tadımı",
                Specialties = "Hakiki Roma Usulü Cacio e Pepe & Taze Cannoli",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Trastevere+Food+Tour+Rome", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = romeHotel.Id,
                Name = "La Pergola (3 Michelin) Panoramik Roma Restoranı",
                Category = "Restaurant",
                SubCategory = "Roma Tepesinde İtalyan Haute Cuisine",
                Description = "Monte Mario tepesinde, Aziz Petrus Bazilikası ve tüm Roma manzarasına bakan, efsanevi şef Heinz Beck yönetiminde İtalya'nın en prestijli masası.",
                DistanceMeters = 5200, WalkingTimeMinutes = 55, Latitude = 41.9180, Longitude = 12.4500,
                Address = "Via Alberto Cadlolo, 101, 00136 Roma", Phone = "+39 06 3509 2152", PriceLevel = 4, Rating = 5.0m, ReviewCount = 850,
                WorkingHours = "19:30 - 23:30", HotelDiscountNote = "Otel Misafirlerine Özel Şef Heinz Beck Karşılama Şampanyası",
                HighlightFeatures = "3 Michelin Yıldızı, 60.000 Şişelik Şarap Mahzeni, Çatıdan Panoramik Roma Manzarası",
                Specialties = "Fagotelli La Pergola (Şefin Efsanevi Makarnası), Trüflü Güvercin Göğsü",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=La+Pergola+Rome", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = romeHotel.Id,
                Name = "Antico Caffè Greco 1760 Tarihi Sanatçı Kahvesi & Bar",
                Category = "Bar",
                SubCategory = "İspanyol Merdivenleri Tarihi Kahvehanesi",
                Description = "1760'tan beri Goethe, Casanova, Byron ve Liszt'in kahve içtiği, antika aynaları ve kırmızı kadife localarıyla Roma'nın en eski kahvesi.",
                DistanceMeters = 1400, WalkingTimeMinutes = 16, Latitude = 41.9056, Longitude = 12.4822,
                Address = "Via dei Condotti, 86, 00187 Roma", Phone = "+39 06 679 1700", PriceLevel = 3, Rating = 4.8m, ReviewCount = 1650,
                WorkingHours = "09:00 - 21:00", HotelDiscountNote = "Hakiki İtalyan Espresso & Tiramisu İkramı",
                HighlightFeatures = "Tarihi Mermer Masalar, 300'den Fazla Orijinal Sanat Eseri, Klasik İtalyan Baristası",
                Specialties = "Özel Harman Greco Espresso, Ev Yapımı Mascarpone Tiramisu",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Antico+Caffe+Greco+Rome", IsPartner = true
            });
        }

        // ─── 15. BALİ AKTİVİTELERİ & MEKANLARI ───
        if (baliHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = baliHotel.Id,
                Name = "Tegallalang Pirinç Terasları & Dev Orman Salıncağı Turu",
                Category = "Activity",
                SubCategory = "Doğa Harikası & Tropikal Salıncak",
                Description = "UNESCO korumasındaki zümrüt pirinç basamakları üzerinde dev salıncakla gökyüzüne süzülme ve geleneksel Subak sulama sistemini keşif turu.",
                DistanceMeters = 8500, WalkingTimeMinutes = 80, Latitude = -8.4350, Longitude = 115.2790,
                Address = "Jl. Raya Tegallalang, Gianyar, Bali 80561", Phone = "+62 361 975 666", PriceLevel = 2, Rating = 4.9m, ReviewCount = 2300,
                WorkingHours = "08:00 - 18:00", HotelDiscountNote = "Otel Misafirlerine %20 İndirim & Ücretsiz Salıncak Fotoğraf Çekimi",
                HighlightFeatures = "Zümrüt Pirinç Vadisi, Dev Salıncak (Jungle Swing), Kuş Yuvası Seyir Noktaları",
                Specialties = "Taze Kokoş Suyu & Bali Kahvesi Luwak Tadımı",
                ImageUrl = "/images/bali_resort.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Tegallalang+Rice+Terrace+Bali", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = baliHotel.Id,
                Name = "Ayung Nehri Vahşi Doğa Rafting & Şelale Macerası",
                Category = "Activity",
                SubCategory = "Akarsu Raftingi & Tropikal Kanyon",
                Description = "Ubud yağmur ormanlarının içinden akan Ayung nehrinde, gizli şelaleler ve Ramayana taş oymaları eşliğinde 12 kilometrelik rafting macerası.",
                DistanceMeters = 1200, WalkingTimeMinutes = 15, Latitude = -8.5000, Longitude = 115.2450,
                Address = "Kedewatan Rafting Pier, Ubud, Gianyar, Bali", Phone = "+62 361 238 759", PriceLevel = 2, Rating = 4.9m, ReviewCount = 1420,
                WorkingHours = "08:30 - 15:30", HotelDiscountNote = "Otelden Nehir Kenarına Ücretsiz Transfer & Endonezya Öğle Büfesi",
                HighlightFeatures = "Kanyon İçi Gizli Şelale Molası, Nehir Boyu Taş Kabartmaları, Güvenli Aile Parkuru",
                Specialties = "Bitişte Nehir Kenarı Organik Endonezya Nasi Goreng Büfesi",
                ImageUrl = "/images/boat_tour.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Ayung+River+Rafting+Ubud", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = baliHotel.Id,
                Name = "Locavore NXT Sürdürülebilir Orman Gastronomi Restoranı",
                Category = "Restaurant",
                SubCategory = "Yerel Bali Otları & Yenilikçi Mutfak",
                Description = "Sadece Endonezya takımadalarında yetişen yerel malzemeleri moleküler ve fermantasyon teknikleriyle birleştiren Asya'nın ödüllü restoranı.",
                DistanceMeters = 3400, WalkingTimeMinutes = 40, Latitude = -8.5150, Longitude = 115.2630,
                Address = "Jl. A.A. Gede Rai No.18, Lodtunduh, Ubud, Bali", Phone = "+62 821 4495 6226", PriceLevel = 4, Rating = 4.9m, ReviewCount = 1180,
                WorkingHours = "12:00 - 15:00 / 18:00 - 22:00", HotelDiscountNote = "Otel Misafirlerine Tadım Menüsünde Özel Kokteyl Eşleşmesi",
                HighlightFeatures = "Sıfır Atık Mutfak, Bahçeden Masaya Konsepti, İnteraktif Açık Mutfak",
                Specialties = "Füme Ördek Yumurtası & Çeltik Mantarı, Çarkıfelek Meyveli Bali Çikolatası",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Locavore+NXT+Bali", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = baliHotel.Id,
                Name = "Potato Head Beach Club Seminyak Gün Batımı & Havuz Barı",
                Category = "Bar",
                SubCategory = "Okyanus Kıyısı Beach Club & Canlı DJ",
                Description = "Hint Okyanusu kıyısında, kavisli ahşap amfi tiyatrosu, sonsuzluk havuzu ve uluslararası DJ performanslarıyla gün batımının ikonik adresi.",
                DistanceMeters = 28000, WalkingTimeMinutes = 60, Latitude = -8.6860, Longitude = 115.1520,
                Address = "Jl. Petitenget No.51B, Seminyak, Kuta Utara, Bali", Phone = "+62 361 620 7979", PriceLevel = 3, Rating = 4.8m, ReviewCount = 3100,
                WorkingHours = "10:00 - 02:00", HotelDiscountNote = "VIP Gündoğumu/Günbatımı Şezlong Rezervasyonu ve Tropikal İçecek",
                HighlightFeatures = "Okyanusa Sıfır Sonsuzluk Havuzu, Canlı DJ Performansları, Eko-Tasarım Mimari",
                Specialties = "Indo 75 İmzalı Tropikal Kokteyl, Taze Hindistan Cevizli Balık Ceviche",
                ImageUrl = "/images/sunset_bar.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Potato+Head+Beach+Club+Bali", IsPartner = true
            });
        }

        // ─── 16. İSVİÇRE ALPLERİ & ZÜRİH AKTİVİTELERİ & MEKANLARI ───
        if (alpsHotel != null)
        {
            TryAdd(new NearbyPlace
            {
                HotelId = alpsHotel.Id,
                Name = "Matterhorn Glacier Paradise Avrupa'nın En Yüksek Teleferiği",
                Category = "Activity",
                SubCategory = "Buzul Sarayı & Zirve Panoraması",
                Description = "3.883 metre yükseklikte, İsviçre, İtalya ve Fransa Alplerinin 38 buzul zirvesini gören açık seyir platformu ve buzdan heykeller sarayı.",
                DistanceMeters = 2500, WalkingTimeMinutes = 28, Latitude = 45.9380, Longitude = 7.7290,
                Address = "Schluhmattstrasse 28, 3920 Zermatt, İsviçre", Phone = "+41 27 966 01 01", PriceLevel = 3, Rating = 5.0m, ReviewCount = 1890,
                WorkingHours = "08:30 - 16:30", HotelDiscountNote = "Otel Misafirlerine %15 İndirim & Buz Sarayı Girişi Dahil",
                HighlightFeatures = "3.883 Metre Yükseklik, 38 Alp Zirvesi Panoraması, Buz İçi Tüneller ve Heykeller",
                Specialties = "Zirvede Sıcak İsviçre Sıcak Çikolatası & Elmalı Strudel",
                ImageUrl = "/images/alps_resort.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Matterhorn+Glacier+Paradise", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = alpsHotel.Id,
                Name = "Zürih Gölü Tarihi Buharlı Vapur Gezisi & İsviçre Fondüsü",
                Category = "Activity",
                SubCategory = "Göl Turu & Alpler Manzarası",
                Description = "Zürih Gölü'nde 100 yıllık tarihi çarklı buharlı vapur ile karlı Alp dağları siluetine karşı göl turu ve geleneksel peynir fondü keyfi.",
                DistanceMeters = 3800, WalkingTimeMinutes = 42, Latitude = 47.3650, Longitude = 8.5420,
                Address = "Bürkliplatz Schiffstation, 8001 Zürih, İsviçre", Phone = "+41 44 487 03 35", PriceLevel = 2, Rating = 4.8m, ReviewCount = 1420,
                WorkingHours = "11:00 - 21:00", HotelDiscountNote = "Birinci Sınıf Güverte Bileti & Sıcak İsviçre Şarabı İkramı",
                HighlightFeatures = "Tarihi Buharlı Çarklı Gemi, Alpler Gölü Yansıması, Canlı Akordeon",
                Specialties = "Gruyère ve Emmental Peynirli Hakiki İsviçre Fondüsü",
                ImageUrl = "/images/boat_tour.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Lake+Zurich+Boat+Cruise", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = alpsHotel.Id,
                Name = "Kronenhalle Zürih Tarihi Sanat Eserleri Eşliğinde Fondü & Yahni",
                Category = "Restaurant",
                SubCategory = "Picasso ve Chagall Tabloları Altında İsviçre Mutfağı",
                Description = "Duvarlarında orijinal Chagall, Picasso ve Miró tablolarının asılı olduğu, dünya sanatçı ve liderlerinin buluştuğu tarihi restoran.",
                DistanceMeters = 1800, WalkingTimeMinutes = 20, Latitude = 47.3670, Longitude = 8.5460,
                Address = "Rämistrasse 4, 8001 Zürih, İsviçre", Phone = "+41 44 262 99 00", PriceLevel = 4, Rating = 4.9m, ReviewCount = 1680,
                WorkingHours = "12:00 - 14:30 / 18:00 - 23:30", HotelDiscountNote = "Otel Misafirlerine Özel Masa & İsviçre Çikolatalı Tatlı İkramı",
                HighlightFeatures = "Orijinal Başyapıt Tablolar, 100 Yıllık Ahşap Salon, Klasik İsviçre Aristokrasisi",
                Specialties = "Zürcher Geschnetzeltes (Kremalı Dana Yahni & Rösti), Trüflü Dağ Peyniri",
                ImageUrl = "/images/aegean_dining.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Kronenhalle+Zurich", IsPartner = true
            });
            TryAdd(new NearbyPlace
            {
                HotelId = alpsHotel.Id,
                Name = "Iglu-Dorf Zermatt Karlar Altında Buz Bar & Aprés-Ski",
                Category = "Bar",
                SubCategory = "Buzdan Yapılmış İglo Bar & Sıcak Şarap Keyfi",
                Description = "Matterhorn manzaralı Rotenboden'de kardan ve buzdan oyularak inşa edilen, koyun postları üzerinde sıcak baharatlı şarap sunan eşsiz buzul barı.",
                DistanceMeters = 4200, WalkingTimeMinutes = 45, Latitude = 45.9800, Longitude = 7.7700,
                Address = "Rotenboden Station, 3920 Zermatt, İsviçre", Phone = "+41 41 612 27 28", PriceLevel = 3, Rating = 4.9m, ReviewCount = 890,
                WorkingHours = "11:00 - 17:00", HotelDiscountNote = "Girişte Özel Kar Kadehinde Sıcak Baharatlı Şarap İkramı",
                HighlightFeatures = "Buzdan Oyulmuş Bar ve Koltuklar, Matterhorn Karşısı, Sıcak Jakuzi Deneyimi",
                Specialties = "Geleneksel Glühwein (Sıcak Baharatlı Şarap), İsviçre Alp Peynir Tabağı",
                ImageUrl = "/images/uludag_ski.jpg", GoogleMapsUrl = "https://maps.google.com/?q=Iglu-Dorf+Zermatt", IsPartner = true
            });
        }

        if (toAdd.Count > 0)
        {
            db.NearbyPlaces.AddRange(toAdd);
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureRoomsForHotelsAsync(AppDb db)
    {
        var hotels = await db.Hotels.ToListAsync();
        var existingFeats = await db.RoomFeatures.ToListAsync();
        int y = DateTime.Today.Year;

        foreach (var h in hotels)
        {
            var hasRooms = await db.RoomTypes.AnyAsync(r => r.HotelId == h.Id);
            if (hasRooms) continue;

            var floor1 = new Floor { HotelId = h.Id, Number = 1, Name = "1. Kat" };
            var floor2 = new Floor { HotelId = h.Id, Number = 2, Name = "2. Kat" };
            db.Floors.AddRange(floor1, floor2);
            await db.SaveChangesAsync();

            List<RoomType> types;
            if (h.City.Contains("Uludağ") || h.City.Contains("Bursa"))
            {
                types = new List<RoomType>
                {
                    new() { HotelId = h.Id, Name = "Standart Dağ Odası", NameEn = "Standard Mountain Room", Description = "Ahşap sıcak alp dağ mimarisi, dağ manzarası.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 2900, AreaM2 = 28, BedType = "Çift kişilik" },
                    new() { HotelId = h.Id, Name = "Deluxe Şömineli Dağ Odası", NameEn = "Deluxe Fireplace Room", Description = "Doğal taş şömineli, sıcak alp chalet tasarımı.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 3900, AreaM2 = 36, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Kar & Pist Manzaralı Oda", NameEn = "Ski Slope View Room", Description = "Uludağ 1. Gelişim pistlerine doğrudan panoramik bakış.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 4600, AreaM2 = 38, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Aile Chalet Süiti", NameEn = "Family Chalet Suite", Description = "4-5 kişilik geniş dubleks aile süiti.", BaseCapacity = 4, MaxCapacity = 5, BasePrice = 5800, AreaM2 = 55, BedType = "1 çift + 2 tek" },
                    new() { HotelId = h.Id, Name = "Termal Jakuzili Zirve Suit", NameEn = "Thermal Jacuzzi Peak Suite", Description = "Özel termal jakuzi ve panoramik çam ormanı manzarası.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 8500, AreaM2 = 75, BedType = "King" }
                };
            }
            else if (h.City.Contains("Fethiye") || h.City.Contains("Ölüdeniz"))
            {
                types = new List<RoomType>
                {
                    new() { HotelId = h.Id, Name = "Standart Bahçe Odası", NameEn = "Standard Garden Room", Description = "Zeytin ağaçları ve begonvillerle çevrili sakin oda.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 2800, AreaM2 = 26, BedType = "Çift kişilik" },
                    new() { HotelId = h.Id, Name = "Deluxe Teraslı Doğa Odası", NameEn = "Deluxe Terrace Nature Room", Description = "Geniş güneşlenme terası ve dağ esintisi.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 3700, AreaM2 = 35, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Ölüdeniz Lagün Manzaralı Oda", NameEn = "Blue Lagoon View Room", Description = "Turkuaz Ölüdeniz lagün manzaralı lüks oda.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 4700, AreaM2 = 38, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Geniş Aile Villası", NameEn = "Family Villa Suite", Description = "Müstakil bahçe girişli 4-5 kişilik ferah villa oda.", BaseCapacity = 4, MaxCapacity = 5, BasePrice = 5600, AreaM2 = 60, BedType = "1 çift + 2 tek" },
                    new() { HotelId = h.Id, Name = "Özel Havuzlu Balayı Suiti", NameEn = "Private Pool Honeymoon Suite", Description = "Odaya özel sonsuzluk havuzu ve jakuzili lüks balayı konsepti.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 8900, AreaM2 = 80, BedType = "King" }
                };
            }
            else if (h.City.Contains("Mardin"))
            {
                types = new List<RoomType>
                {
                    new() { HotelId = h.Id, Name = "Klasik Tarihi Taş Oda", NameEn = "Classic Stone Room", Description = "Geleneksel sarı kalker taş mimarisi ve yüksek tavan.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 2600, AreaM2 = 28, BedType = "Çift kişilik" },
                    new() { HotelId = h.Id, Name = "Deluxe Kemerli Taş Oda", NameEn = "Deluxe Arched Stone Room", Description = "Artuklu dönemi kemerli taş tavanlar, el dokuması kilimler.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 3500, AreaM2 = 36, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Mezopotamya Manzaralı Balkonlu Oda", NameEn = "Mesopotamia View Balcony Room", Description = "Uçsuz bucaksız altın Mezopotamya ovası panoraması.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 4300, AreaM2 = 38, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Geleneksel Konak Aile Süiti", NameEn = "Traditional Mansion Family Suite", Description = "Tarihi eyvanlı, 4 kişilik geniş taş avlulu süit.", BaseCapacity = 4, MaxCapacity = 5, BasePrice = 5200, AreaM2 = 58, BedType = "1 çift + 2 tek" },
                    new() { HotelId = h.Id, Name = "Tarihi Paşa Dairesi & Jakuzili Suit", NameEn = "Historical Pasha Jacuzzi Suite", Description = "Tarihi taş kemer içi jakuzi, özel teras ve krallara layık lüks.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 7800, AreaM2 = 72, BedType = "King" }
                };
            }
            else if (h.City.Contains("İstanbul"))
            {
                types = new List<RoomType>
                {
                    new() { HotelId = h.Id, Name = "Standart Şehir Odası", NameEn = "Standard City Room", Description = "Şık İstanbul manzaralı, konforlu oda.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 3100, AreaM2 = 27, BedType = "Çift kişilik" },
                    new() { HotelId = h.Id, Name = "Deluxe Bosphorus Oda", NameEn = "Deluxe Bosphorus Room", Description = "Tarihi Boğaziçi yalı manzaralı deluxe oda.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 4500, AreaM2 = 36, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Boğaz Manzaralı Balkonlu Oda", NameEn = "Bosphorus View Balcony Room", Description = "Boğaz köprüsü ve saray manzaralı ferah balkon.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 5400, AreaM2 = 40, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Boğaziçi Aile Süiti", NameEn = "Bosphorus Family Suite", Description = "2 ayrı yatak odalı, 4 kişilik geniş rezidans süiti.", BaseCapacity = 4, MaxCapacity = 5, BasePrice = 6400, AreaM2 = 62, BedType = "1 çift + 2 tek" },
                    new() { HotelId = h.Id, Name = "Sultan Saray Suiti & Jakuzi", NameEn = "Sultan Palace Jacuzzi Suite", Description = "Osmanlı ihtişamı, Boğaz panoraması ve jakuzi.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 9800, AreaM2 = 85, BedType = "King" }
                };
            }
            else if (h.City.Contains("Kapadokya") || h.City.Contains("Nevşehir"))
            {
                types = new List<RoomType>
                {
                    new() { HotelId = h.Id, Name = "Klasik Kaya Mağara Oda", NameEn = "Classic Cave Room", Description = "Doğal tüf kayaya oyulmuş otantik serin mağara oda.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 2900, AreaM2 = 28, BedType = "Çift kişilik" },
                    new() { HotelId = h.Id, Name = "Deluxe Şömineli Mağara Oda", NameEn = "Deluxe Fireplace Cave Room", Description = "Doğal taş şömineli, romantik cave deluxe.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 3900, AreaM2 = 36, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Balon Manzaralı Teraslı Oda", NameEn = "Hot Air Balloon View Room", Description = "Sabah gündoğumu balon uçuşlarını izleyen özel teras.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 4900, AreaM2 = 40, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Kapadokya Kemerli Aile Suiti", NameEn = "Cappadocia Family Suite", Description = "Geniş yaşam alanlı 4-5 kişilik otantik taş süit.", BaseCapacity = 4, MaxCapacity = 5, BasePrice = 5900, AreaM2 = 65, BedType = "1 çift + 2 tek" },
                    new() { HotelId = h.Id, Name = "Kral Mağara Suiti & Jakuzi", NameEn = "King Cave Jacuzzi Suite", Description = "Kaya içi ısıtmalı jakuzi, şömine ve panoramik vadi manzarası.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 8800, AreaM2 = 85, BedType = "King" }
                };
            }
            else if (h.City.Contains("Paris") || h.City.Contains("Fransa"))
            {
                types = new List<RoomType>
                {
                    new() { HotelId = h.Id, Name = "Klasik Haussmann Deluxe Oda", NameEn = "Classic Haussmann Deluxe Room", Description = "Yüksek tavan, Fransız balkon, klasik Paris şıklığı ve parke zemin.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 4800, AreaM2 = 30, BedType = "Çift kişilik" },
                    new() { HotelId = h.Id, Name = "Place Vendôme Panoramik Oda", NameEn = "Place Vendôme View Room", Description = "Tarihi Vendôme sütununa ve lüks meydana bakan şık Fransız mimarisi.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 6200, AreaM2 = 38, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Eyfel Manzaralı Teraslı Süit", NameEn = "Eiffel View Terrace Suite", Description = "Özel terasta Eyfel Kulesi ve Seine nehri manzarası eşliğinde kahvaltı.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 7900, AreaM2 = 46, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Parisienne Zerafet Aile Süiti", NameEn = "Parisienne Family Suite", Description = "2 ayrı yatak odalı, Fransız aristokrasisini yansıtan ferah aile süiti.", BaseCapacity = 4, MaxCapacity = 5, BasePrice = 9200, AreaM2 = 65, BedType = "1 çift + 2 tek" },
                    new() { HotelId = h.Id, Name = "Royal Vendôme Penthouse & Şampanya Süiti", NameEn = "Royal Vendôme Penthouse Suite", Description = "Panoramik Paris çatısı, mermer jakuzi ve özel uşak servisi.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 14500, AreaM2 = 95, BedType = "King" }
                };
            }
            else if (h.City.Contains("Londra") || h.City.Contains("İngiltere") || h.City.Contains("London"))
            {
                types = new List<RoomType>
                {
                    new() { HotelId = h.Id, Name = "Mayfair Klasik Kraliyet Odası", NameEn = "Mayfair Classic Royal Room", Description = "İngiliz zarafeti, şömineli sıcak lüks atmosfer ve kadife detaylar.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 4600, AreaM2 = 30, BedType = "Çift kişilik" },
                    new() { HotelId = h.Id, Name = "Westminster Deluxe Oda", NameEn = "Westminster Deluxe Room", Description = "Görkemli Whitehall ve tarihi Londra sokak manzarası.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 5900, AreaM2 = 38, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Big Ben & Thames Manzaralı Oda", NameEn = "Big Ben & Thames View Room", Description = "Thames Nehri ve Big Ben saat kulesi silueti manzarası.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 7500, AreaM2 = 45, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Georgian Geniş Rezidans Aile Süiti", NameEn = "Georgian Residence Family Suite", Description = "İngiliz soylu evi tarzında döşenmiş 4-5 kişilik ferah süit.", BaseCapacity = 4, MaxCapacity = 5, BasePrice = 8800, AreaM2 = 68, BedType = "1 çift + 2 tek" },
                    new() { HotelId = h.Id, Name = "The Royal Sovereign Penthouse Suite", NameEn = "Royal Sovereign Penthouse Suite", Description = "Kraliyet süiti, mermer banyo, jakuzi ve özel ikindi çayı servisi.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 13800, AreaM2 = 100, BedType = "King" }
                };
            }
            else if (h.City.Contains("New York") || h.City.Contains("ABD") || h.City.Contains("Manhattan"))
            {
                types = new List<RoomType>
                {
                    new() { HotelId = h.Id, Name = "Manhattan Deluxe Oda", NameEn = "Manhattan Deluxe Room", Description = "Modern metropol tasarımı, ferah New York şehir ışıkları ve king yatak.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 5200, AreaM2 = 32, BedType = "Çift kişilik" },
                    new() { HotelId = h.Id, Name = "Central Park Manzaralı Köşe Oda", NameEn = "Central Park View Corner Room", Description = "Central Park ağaçları ve 5th Avenue üzerinde panoramik köşe manzara.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 6800, AreaM2 = 42, BedType = "King" },
                    new() { HotelId = h.Id, Name = "5th Avenue Executive Suite", NameEn = "5th Avenue Executive Suite", Description = "Ayrı çalışma ve oturma alanı, VIP lounge erişimi.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 8400, AreaM2 = 55, BedType = "King" },
                    new() { HotelId = h.Id, Name = "TriBeCa Loft Aile Süiti", NameEn = "TriBeCa Loft Family Suite", Description = "Yüksek tavanlı modern endüstriyel lüks, 4-5 kişilik tasarım süit.", BaseCapacity = 4, MaxCapacity = 5, BasePrice = 9900, AreaM2 = 75, BedType = "1 çift + 2 tek" },
                    new() { HotelId = h.Id, Name = "Empire State Penthouse & Teras Suiti", NameEn = "Empire State Penthouse Terrace Suite", Description = "Manhattan gökdelenleri ayaklarınızın altında, özel gökyüzü jakuzisi.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 15800, AreaM2 = 110, BedType = "King" }
                };
            }
            else if (h.City.Contains("Dubai") || h.City.Contains("BAE"))
            {
                types = new List<RoomType>
                {
                    new() { HotelId = h.Id, Name = "Palm Marina Deluxe Oda", NameEn = "Palm Marina Deluxe Room", Description = "Arap Körfezi ve marina yat limanı esintisi, mermer banyo.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 4900, AreaM2 = 45, BedType = "Çift kişilik" },
                    new() { HotelId = h.Id, Name = "Burj & Körfez Manzaralı Lüks Oda", NameEn = "Burj & Arabian Gulf Luxury Room", Description = "Gündoğumu ve gün batımında parıldayan Körfez suları manzarası.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 6500, AreaM2 = 55, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Palm Jumeirah Lagoon Suite", NameEn = "Palm Jumeirah Lagoon Suite", Description = "Özel lagün havuzuna doğrudan çıkış ve güneşlenme terası.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 8600, AreaM2 = 70, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Özel Havuzlu Çöl & Su Villası", NameEn = "Private Pool Water & Desert Villa", Description = "Kendi özel havuzu ve plaj alanı olan bağımsız lüks villa.", BaseCapacity = 4, MaxCapacity = 5, BasePrice = 11500, AreaM2 = 120, BedType = "1 çift + 2 tek" },
                    new() { HotelId = h.Id, Name = "Royal Mirage Sultan Penthouse Suite", NameEn = "Royal Mirage Sultan Penthouse", Description = "Altın varak detaylar, 360 derece körfez manzarası ve VIP şoförlü transfer.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 16900, AreaM2 = 150, BedType = "King" }
                };
            }
            else if (h.City.Contains("Tokyo") || h.City.Contains("Japonya"))
            {
                types = new List<RoomType>
                {
                    new() { HotelId = h.Id, Name = "Modern Zen Minimalist Oda", NameEn = "Modern Zen Minimalist Room", Description = "Geleneksel Japon sedir ağacı ve modern dinginlik.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 4400, AreaM2 = 28, BedType = "Çift kişilik" },
                    new() { HotelId = h.Id, Name = "Sakura Bahçe Manzaralı Deluxe", NameEn = "Sakura Imperial Garden Deluxe", Description = "İmparatorluk Sarayı koruları ve kiraz çiçekleri manzarası.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 5600, AreaM2 = 36, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Geleneksel Tatami & Özel Onsen Odası", NameEn = "Traditional Tatami & Onsen Suite", Description = "Hakiki tatami hasırı, Hinoki sedir ağacı küveti ve çay köşesi.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 7200, AreaM2 = 48, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Tokyo Tower Manzaralı Executive Suite", NameEn = "Tokyo Tower View Executive Suite", Description = "Işıltılı Tokyo kulesi ve gökdelen silueti manzarası.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 8500, AreaM2 = 62, BedType = "King" },
                    new() { HotelId = h.Id, Name = "İmparatorluk Panoramik Saray Suiti", NameEn = "Imperial Palace Panoramic Suite", Description = "Japon sanat eserleri, özel çay seremonisi alanı ve jakuzili lüks banyo.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 13500, AreaM2 = 95, BedType = "King" }
                };
            }
            else if (h.City.Contains("Roma") || h.City.Contains("İtalya") || h.City.Contains("Rome"))
            {
                types = new List<RoomType>
                {
                    new() { HotelId = h.Id, Name = "Klasik Roma Taş & Fresko Oda", NameEn = "Classic Roman Stone & Fresco Room", Description = "Tarihi tavan freskleri, İtalyan mermeri ve sıcak zarafet.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 3800, AreaM2 = 30, BedType = "Çift kişilik" },
                    new() { HotelId = h.Id, Name = "Colosseum Vista Deluxe Balkonlu", NameEn = "Colosseum Vista Deluxe Balcony", Description = "Doğrudan Roma Kolezyumu kalıntılarına bakan romantik balkon.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 5200, AreaM2 = 38, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Piazza Navona Aile Süiti", NameEn = "Piazza Navona Family Suite", Description = "Geniş İtalyan palazzo salonu, 4-5 kişilik aile konforu.", BaseCapacity = 4, MaxCapacity = 5, BasePrice = 6400, AreaM2 = 60, BedType = "1 çift + 2 tek" },
                    new() { HotelId = h.Id, Name = "Antik Mermer Jakuzili Sezar Suiti", NameEn = "Caesar Jacuzzi Ancient Marble Suite", Description = "Özel Carrara mermeri jakuzi, Roma sütunları ve lüks şömine.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 8200, AreaM2 = 72, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Villa Borghese Panoramik Çatı Suiti", NameEn = "Villa Borghese Rooftop Suite", Description = "Roma'nın 7 tepesini izleyen özel teras ve gün batımı kokteyl alanı.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 11800, AreaM2 = 90, BedType = "King" }
                };
            }
            else if (h.City.Contains("Bali") || h.City.Contains("Endonezya"))
            {
                types = new List<RoomType>
                {
                    new() { HotelId = h.Id, Name = "Tropikal Yağmur Ormanı Deluxe", NameEn = "Tropical Rainforest Deluxe", Description = "Açık hava duşu, tropikal kuş sesleri ve tik ağacı mobilyalar.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 3200, AreaM2 = 42, BedType = "Çift kişilik" },
                    new() { HotelId = h.Id, Name = "Ubud Pirinç Terası Manzaralı Oda", NameEn = "Ubud Rice Terrace View Room", Description = "Zümrüt yeşili pirinç vadisine bakan geniş ahşap teras.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 4400, AreaM2 = 50, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Özel Sonsuzluk Havuzlu Balayı Villası", NameEn = "Private Pool Honeymoon Villa", Description = "Yağmur ormanı üzerinde asılı özel havuz ve çift kişilik jakuzi.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 6500, AreaM2 = 85, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Nehir Kıyısı Bambu Aile Köşkü", NameEn = "Riverside Bamboo Family Pavilion", Description = "Ayung nehri kenarında 2 katlı çevre dostu lüks bambu villa.", BaseCapacity = 4, MaxCapacity = 5, BasePrice = 7800, AreaM2 = 110, BedType = "1 çift + 2 tek" },
                    new() { HotelId = h.Id, Name = "Lotus Sanctuary Kraliyet Villası", NameEn = "Lotus Sanctuary Royal Villa", Description = "Özel tapınak bahçesi, uşak servisi, yoga çardağı ve sonsuzluk havuzu.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 12500, AreaM2 = 160, BedType = "King" }
                };
            }
            else if (h.City.Contains("İsviçre") || h.City.Contains("Alp") || h.City.Contains("Zürih") || h.City.Contains("Swiss"))
            {
                types = new List<RoomType>
                {
                    new() { HotelId = h.Id, Name = "Matterhorn Kar & Çam Odası", NameEn = "Matterhorn Snow & Pine Room", Description = "İsviçre çamı kokusu, sıcak yün tekstiller ve dağ manzarası.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 4700, AreaM2 = 32, BedType = "Çift kişilik" },
                    new() { HotelId = h.Id, Name = "Şömineli Lüks Alp Chalet Odası", NameEn = "Luxury Fireplace Alpine Chalet", Description = "Özel odun şöminesi, kar manzaralı kış bahçesi balkonu.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 6200, AreaM2 = 42, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Buzul & Zürih Gölü Manzaralı Teraslı Süit", NameEn = "Glacier & Lake View Terrace Suite", Description = "Karlı zirveler ve buzul gölü panoraması sunan geniş teras.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 7800, AreaM2 = 55, BedType = "King" },
                    new() { HotelId = h.Id, Name = "İsviçre Çam Ağacı Dubleks Aile Chalet'si", NameEn = "Swiss Pine Duplex Family Chalet", Description = "Çocuklar için tavan arası çatı katı, 5 kişilik geniş dağ evi.", BaseCapacity = 4, MaxCapacity = 5, BasePrice = 9400, AreaM2 = 80, BedType = "1 çift + 2 tek" },
                    new() { HotelId = h.Id, Name = "Zirve Panoramik Termal Havuzlu Penthouse", NameEn = "Peak Panoramic Thermal Pool Penthouse", Description = "Balkonda ısıtmalı açık hava termal jakuzisi, fondü masası ve şömine.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 14900, AreaM2 = 105, BedType = "King" }
                };
            }
            else
            {
                types = new List<RoomType>
                {
                    new() { HotelId = h.Id, Name = "Standart Resort Oda", NameEn = "Standard Resort Room", Description = "Modern tasarım, bahçe manzaralı tatil odası.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 2700, AreaM2 = 26, BedType = "Çift kişilik" },
                    new() { HotelId = h.Id, Name = "Deluxe Havuzlu Oda", NameEn = "Deluxe Pool View Room", Description = "Lagün havuz manzaralı geniş balkonlu oda.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 3600, AreaM2 = 34, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Panoramik Deniz Manzaralı Oda", NameEn = "Panoramic Sea View Room", Description = "Akdeniz'in masmavi sonsuzluğuna bakan premium oda.", BaseCapacity = 2, MaxCapacity = 3, BasePrice = 4500, AreaM2 = 38, BedType = "King" },
                    new() { HotelId = h.Id, Name = "Resort Aile Odası", NameEn = "Resort Family Room", Description = "Ara kapılı, çocuklu ailelere özel 4 kişilik oda.", BaseCapacity = 4, MaxCapacity = 5, BasePrice = 5400, AreaM2 = 52, BedType = "1 çift + 2 tek" },
                    new() { HotelId = h.Id, Name = "Executive Jakuzili Deniz Suiti", NameEn = "Executive Jacuzzi Sea Suite", Description = "Balkonda özel jakuzi, VIP servis ve lounge erişimi.", BaseCapacity = 2, MaxCapacity = 4, BasePrice = 8200, AreaM2 = 75, BedType = "King" }
                };
            }

            db.RoomTypes.AddRange(types);
            await db.SaveChangesAsync();

            if (existingFeats.Count > 0)
            {
                for (int i = 0; i < types.Count; i++)
                {
                    foreach (var f in existingFeats.Take(3))
                    {
                        db.RoomTypeFeatures.Add(new RoomTypeFeature { RoomTypeId = types[i].Id, FeatureId = f.Id });
                    }
                }
            }

            var rooms = new List<Room>();
            int roomNum = 101;
            foreach (var t in types)
            {
                rooms.Add(new Room { HotelId = h.Id, FloorId = floor1.Id, RoomTypeId = t.Id, Number = roomNum++.ToString(), Status = RoomStatus.Available, Housekeeping = HkStatus.Clean });
                rooms.Add(new Room { HotelId = h.Id, FloorId = floor2.Id, RoomTypeId = t.Id, Number = (roomNum + 100).ToString(), Status = RoomStatus.Available, Housekeeping = HkStatus.Clean });
            }
            db.Rooms.AddRange(rooms);

            foreach (var t in types)
            {
                db.SeasonalRates.Add(new SeasonalRate { RoomTypeId = t.Id, Name = "Yaz Sezonu", StartDate = new DateTime(y, 6, 1), EndDate = new DateTime(y, 9, 15), PricePerNight = Math.Round(t.BasePrice * 1.4m), ExtraAdultPrice = 800, ChildPrice = 400, MinStay = 2 });
                db.SeasonalRates.Add(new SeasonalRate { RoomTypeId = t.Id, Name = "Ara Sezon", StartDate = new DateTime(y, 4, 1), EndDate = new DateTime(y, 5, 31), PricePerNight = t.BasePrice, ExtraAdultPrice = 600, ChildPrice = 300, MinStay = 1 });
                db.SeasonalRates.Add(new SeasonalRate { RoomTypeId = t.Id, Name = "Kış Sezonu", StartDate = new DateTime(y, 10, 16), EndDate = new DateTime(y + 1, 3, 31), PricePerNight = Math.Round(t.BasePrice * 0.9m), ExtraAdultPrice = 500, ChildPrice = 250, MinStay = 1 });
            }
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureOperationalDataForHotelsAsync(AppDb db)
    {
        var hotels = await db.Hotels.ToListAsync();
        if (hotels.Count == 0) return;

        var allGuests = await db.Guests.Take(100).ToListAsync();
        if (allGuests.Count == 0) return;

        var boards = await db.BoardTypes.ToListAsync();
        var plans = await db.RatePlans.ToListAsync();
        var channels = await db.Channels.ToListAsync();
        var pms = await db.PaymentMethods.ToListAsync();
        var cash = await db.CashRegisters.FirstOrDefaultAsync();
        var cashRegId = cash?.Id ?? 1;

        var rnd = new Random(101);
        var today = DateTime.Today;

        foreach (var h in hotels)
        {
            var hotelRooms = await db.Rooms.Where(r => r.HotelId == h.Id).Include(r => r.RoomType).ToListAsync();
            if (hotelRooms.Count == 0) continue;

            bool hasReservations = await db.ReservationRooms.AnyAsync(rr => rr.Room!.HotelId == h.Id);
            if (hasReservations) continue;

            var seasonalRates = await db.SeasonalRates.Where(sr => hotelRooms.Select(r => r.RoomTypeId).Contains(sr.RoomTypeId)).ToListAsync();
            var occupancy = new List<(int roomId, DateTime a, DateTime b)>();
            int seq = h.Id * 1000;

            for (int i = 0; i < 35; i++)
            {
                var room = hotelRooms[rnd.Next(hotelRooms.Count)];
                var type = room.RoomType;
                if (type == null) continue;

                int startOffset = rnd.Next(-25, 25);
                var ci = today.AddDays(startOffset);
                var nights = rnd.Next(2, 6);
                var co = ci.AddDays(nights);

                if (occupancy.Any(o => o.roomId == room.Id && ci < o.b && co > o.a))
                    continue;

                occupancy.Add((room.Id, ci, co));

                var board = boards.Count > 0 ? boards[rnd.Next(boards.Count)] : null;
                var adults = rnd.Next(1, 3);
                decimal basePrice = type.BasePrice;
                decimal nightly = seasonalRates.FirstOrDefault(s => s.RoomTypeId == type.Id && ci >= s.StartDate && ci <= s.EndDate)?.PricePerNight ?? basePrice;
                decimal total = (nightly + (board?.PerPersonPerNight ?? 0) * adults) * nights;

                ResStatus st = co <= today ? ResStatus.CheckedOut : (ci <= today ? ResStatus.CheckedIn : ResStatus.Confirmed);
                if (rnd.Next(0, 15) == 0) st = ResStatus.Cancelled;

                var g = allGuests[rnd.Next(allGuests.Count)];
                var ch = channels.Count > 0 && rnd.Next(0, 10) < 7 ? channels[rnd.Next(channels.Count)] : null;

                var res = new Reservation
                {
                    Code = $"RZ{h.Id}{++seq}",
                    GuestId = g.Id,
                    ChannelId = ch?.Id,
                    BoardTypeId = board?.Id ?? 1,
                    RatePlanId = plans.Count > 0 ? plans[rnd.Next(plans.Count)].Id : 1,
                    Status = st,
                    CheckIn = ci,
                    CheckOut = co,
                    Adults = adults,
                    Children = rnd.Next(0, 3) == 0 ? 1 : 0,
                    TotalAmount = total,
                    CreatedAt = ci.AddDays(-rnd.Next(2, 20))
                };

                res.Rooms.Add(new ReservationRoom
                {
                    RoomTypeId = type.Id,
                    RoomId = room.Id,
                    CheckIn = ci,
                    CheckOut = co,
                    NightlyRate = Math.Round(total / nights),
                    Adults = adults
                });

                res.Guests.Add(new ReservationGuest
                {
                    GuestId = g.Id,
                    IsPrimary = true
                });

                if (st != ResStatus.Cancelled && st != ResStatus.NoShow)
                {
                    var folio = new Folio { IsClosed = st == ResStatus.CheckedOut };
                    folio.Charges.Add(new FolioCharge
                    {
                        Description = $"Konaklama {nights} gece ({type.Name})",
                        Category = ChargeCategory.Room,
                        Quantity = 1,
                        UnitPrice = total,
                        TaxRate = 10,
                        Date = ci
                    });

                    if (st is ResStatus.CheckedIn or ResStatus.CheckedOut && rnd.Next(0, 2) == 0)
                    {
                        folio.Charges.Add(new FolioCharge
                        {
                            Description = "Restoran & Room Service",
                            Category = ChargeCategory.Restaurant,
                            Quantity = 1,
                            UnitPrice = rnd.Next(350, 1200),
                            TaxRate = 10,
                            Date = ci.AddDays(1)
                        });
                    }

                    if (st == ResStatus.CheckedOut || rnd.Next(0, 2) == 0)
                    {
                        folio.Payments.Add(new Payment
                        {
                            PaymentMethodId = pms.Count > 0 ? pms[rnd.Next(pms.Count)].Id : 1,
                            CashRegisterId = cashRegId,
                            Amount = st == ResStatus.CheckedOut ? total : Math.Round(total * 0.4m),
                            Date = st == ResStatus.CheckedOut ? co : ci
                        });
                    }

                    res.Folio = folio;
                }

                db.Reservations.Add(res);

                if (st == ResStatus.CheckedIn)
                {
                    room.Housekeeping = rnd.Next(0, 3) == 0 ? HkStatus.Dirty : HkStatus.Clean;
                }
            }

            int dirtyCount = 0;
            foreach (var r in hotelRooms)
            {
                if (rnd.Next(0, 4) == 0 && dirtyCount < 3)
                {
                    r.Housekeeping = HkStatus.Dirty;
                    db.HousekeepingTasks.Add(new HousekeepingTask
                    {
                        RoomId = r.Id,
                        Type = TaskType.DailyClean,
                        Status = WorkStatus.Open,
                        Date = today
                    });
                    dirtyCount++;
                }
            }

            if (hotelRooms.Count > 2)
            {
                var maintRoom = hotelRooms[rnd.Next(hotelRooms.Count)];
                db.MaintenanceRequests.Add(new MaintenanceRequest
                {
                    RoomId = maintRoom.Id,
                    Title = $"{h.Name} - Oda {maintRoom.Number} Klima/Isıtma Kontrolü",
                    Priority = Priority.Normal,
                    Status = WorkStatus.Open,
                    Description = "Rutin sezon öncesi kontrol ve filtre temizliği"
                });
            }

            var activeChannels = channels.Where(c => c.IsConnected).Take(4).ToList();
            var distinctTypes = hotelRooms.Select(r => r.RoomTypeId).Distinct().ToList();
            foreach (var ch in activeChannels)
            {
                foreach (var tId in distinctTypes)
                {
                    var count = hotelRooms.Count(r => r.RoomTypeId == tId);
                    var tObj = hotelRooms.First(r => r.RoomTypeId == tId).RoomType;
                    decimal p = tObj?.BasePrice ?? 3000m;
                    for (int d = 0; d < 30; d++)
                    {
                        db.ChannelInventory.Add(new ChannelInventory
                        {
                            ChannelId = ch.Id,
                            RoomTypeId = tId,
                            Date = today.AddDays(d),
                            Allotment = Math.Max(0, count - rnd.Next(0, 2)),
                            Price = p,
                            MinStay = 1
                        });
                    }
                }
            }

            await db.SaveChangesAsync();
        }
    }
}
