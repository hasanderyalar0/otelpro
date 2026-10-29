using Microsoft.EntityFrameworkCore;
using Otel.Web.Data;

namespace Otel.Web.Services;

public record WeatherInfo(string City, int TempC, string Condition, string Icon, int Humidity, string Wind, int UvIndex, string Advice);

public class ItineraryStop
{
    public int Id { get; set; }
    public int Day { get; set; } = 1;
    public string TimeSlot { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string CategoryIcon { get; set; } = "📍";
    public string Description { get; set; } = "";
    public string Specialty { get; set; } = "";
    public string ConciergeTip { get; set; } = "";
    public string DistanceText { get; set; } = "";
    public string TransitText { get; set; } = "";
    public decimal EstCost { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string ImageUrl { get; set; } = "";
    public string Address { get; set; } = "";
    public decimal Rating { get; set; } = 4.9m;
    public string BadgeColor { get; set; } = "#38bdf8";
}

public class TourGuideProfile
{
    public string Name { get; set; } = "Kaan Demir";
    public string Title { get; set; } = "Bölge Baş Rehberi & Kültür Mirası Uzmanı";
    public string Badge { get; set; } = "TUREB Lisanslı AI Rehber";
    public string Avatar { get; set; } = "🧑‍💼";
    public string Experience { get; set; } = "14 Yıl Bölge Deneyimi";
    public string Rating { get; set; } = "4.99 / 5.0 (2.150+ Misafir Yorumu)";
    public string Languages { get; set; } = "Türkçe, English, Deutsch, Français";
    public string Status { get; set; } = "🟢 Çevrimiçi · Rotanızı Anlık Takip Ediyor";
    public string PersonalizedWelcome { get; set; } = "";
    public string PhoneDirect { get; set; } = "+90 (532) 410 88 00";
}

public class TripPlan
{
    public Hotel Hotel { get; set; } = new();
    public string Profile { get; set; } = "romantic";
    public string ProfileTitle { get; set; } = "Romantik Çift & Balayı";
    public int Days { get; set; } = 1;
    public string Pace { get; set; } = "relaxed";
    public string Budget { get; set; } = "luxury";
    public DateTime StartDate { get; set; } = DateTime.Today;
    public int Adults { get; set; } = 2;
    public int Children { get; set; } = 0;
    public string Transport { get; set; } = "vip";
    public string TransportTitle { get; set; } = "Özel VIP Şoförlü Araç";
    public List<string> SpecialPreferences { get; set; } = new();
    public string? CustomAiPrompt { get; set; }
    public WeatherInfo Weather { get; set; } = new("Antalya", 24, "Güneşli & Açık", "☀️", 42, "9 km/s GB", 6, "Açık hava için harika bir gün.");
    public List<string> PackingTips { get; set; } = new();
    public TourGuideProfile Guide { get; set; } = new();
    public List<ItineraryStop> Stops { get; set; } = new();
    public decimal TotalCostPerPerson => Stops.Sum(s => s.EstCost);
    public double TotalDistanceKm { get; set; } = 12.4;
    public string GeneratedAt { get; set; } = DateTime.Now.ToString("dd MMMM yyyy, HH:mm");
}

public class TripPlannerService(IDbContextFactory<AppDb> dbFactory, NearbyService nearbySvc)
{
    public async Task<List<Hotel>> GetHotelsAsync()
    {
        return await nearbySvc.GetHotelsAsync();
    }

    public async Task<TripPlan> GenerateItineraryAsync(
        int hotelId, 
        string profile, 
        int days, 
        string pace, 
        string budget,
        DateTime? startDate = null,
        int adults = 2,
        int children = 0,
        string transport = "vip",
        List<string>? specialPreferences = null,
        string? customAiPrompt = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var hotel = await db.Hotels.FirstOrDefaultAsync(h => h.Id == hotelId) 
                    ?? await db.Hotels.FirstOrDefaultAsync() 
                    ?? new Hotel { Id = 1, Name = "OtelPro Grand Palace", City = "Antalya", Latitude = 36.8550, Longitude = 30.7950 };

        var transTitle = transport switch
        {
            "vip" => "🚗 Özel VIP Şoförlü Mercedes Vito",
            "taxi" => "🚕 Yerel Taksi & Yürüyüş",
            "car" => "🏎️ Kiralık SUV / Cabrio Araç",
            "walk" => "🚶 Doğa Yürüyüşü & Toplu Taşıma",
            _ => "🚗 Özel Transfer"
        };

        var plan = new TripPlan
        {
            Hotel = hotel,
            Profile = profile,
            Days = Math.Clamp(days, 1, 3),
            Pace = pace,
            Budget = budget,
            StartDate = startDate ?? DateTime.Today,
            Adults = Math.Max(1, adults),
            Children = Math.Max(0, children),
            Transport = transport,
            TransportTitle = transTitle,
            SpecialPreferences = specialPreferences ?? new(),
            CustomAiPrompt = customAiPrompt
        };

        plan.ProfileTitle = profile switch
        {
            "romantic" => "💑 Romantik Çift & Balayı Rotası",
            "family" => "👨‍👩‍👧‍👦 Çocuklu Aile & Güvenli Keşif",
            "history" => "🏛️ Tarih, Kültür & Arkeoloji Gezisi",
            "adventure" => "🏄‍♂️ Macera, Adrenalin & Doğa Keşfi",
            "gourmet" => "🍽️ Gurme Gastronomi & Şarap Tadımı",
            "wellness" => "🧘 Wellness, Spa & Huzur Molası",
            _ => "✨ Özel Seçkili Günlük Rota"
        };

        var city = hotel.City ?? "Antalya";

        // Hava durumu, valiz tavsiyesi ve Kişisel Tur Rehberi
        plan.Weather = GenerateWeatherForCity(city);
        plan.PackingTips = GeneratePackingTips(city, profile);
        plan.Guide = GenerateTourGuideForCity(city, profile, hotel);

        if (!string.IsNullOrWhiteSpace(customAiPrompt))
        {
            plan.PackingTips.Insert(0, $"✨ Özel AI İstek Notu: \"{customAiPrompt.Trim()}\" (Concierge & karşılama ekibimiz bu talebinize göre hazırlandı)");
        }

        // Şehre ve profile göre dakikası dakikasına optimize duraklar
        plan.Stops = GenerateStopsForCityAndProfile(hotel, city, profile, plan.Days, pace, budget);
        plan.TotalDistanceKm = Math.Round(plan.Stops.Count * 3.2 + (city.Contains("Mardin") || city.Contains("Kapadokya") ? 6.5 : 4.0), 1);

        return plan;
    }

    public Task<string> AskGuideAboutStopAsync(TourGuideProfile guide, ItineraryStop stop, string question, TripPlan plan)
    {
        var qLower = (question ?? "").ToLowerInvariant().Trim();
        string answer;

        if (qLower.Contains("giy") || qLower.Contains("kıyafet") || qLower.Contains("ayakkabı") || qLower.Contains("kombin"))
        {
            if (stop.Category.Contains("Activity") || stop.Title.Contains("Rafting") || stop.Title.Contains("Sörf") || stop.Title.Contains("Zipline") || stop.Title.Contains("Safari") || stop.Title.Contains("Dalış"))
            {
                answer = $"Bu heyecan dolu aktivitemiz için hafif, çabuk kuruyan spor giysiler ve kaymaz tabanlı deniz/spor ayakkabısı tercih etmelisiniz. Yanınıza sırt çantasında yedek tişört, güneş kremi ve su geçirmez telefon kılıfı almanızı öneririm. Otelimiz transfer aracında misafirlerimize özel taze havlu ve içecek hazır bulunmaktadır.";
            }
            else if (stop.Category.Contains("Restaurant") || stop.Title.Contains("Michelin") || stop.Title.Contains("Şef") || stop.Title.Contains("Mum Işığı") || stop.Title.Contains("Degustasyon") || stop.Title.Contains("Teras"))
            {
                answer = $"Burası seçkin bir gurme & akşam yemeği durağı olduğundan 'Smart Casual' (şık spor) kıyafet kodu uygulanmaktadır. Beyler için keten veya kumaş pantolon ve kapalı ayakkabı; hanımefendiler için yazlık şık bir elbise ya da blazer kombini çok yakışacaktır. Akşam serinliği için hafif bir şal veya ceket bulundurmanız iyi olur.";
            }
            else if (stop.Category.Contains("Culture") || stop.Title.Contains("Manastır") || stop.Title.Contains("Cami") || stop.Title.Contains("Müze") || stop.Title.Contains("Antik"))
            {
                answer = $"Tarihi ve arkeolojik mekanlarda rahat adım atabilmeniz için kesinlikle topuksuz, kaymaz tabanlı yürüyüş ayakkabısı şarttır. Güneşten korunmak için şapka ve güneş gözlüğü tavsiye ederim. Kutsal mekan geçişlerinde omuz ve dizleri örten rahat giysiler tercih edilmelidir.";
            }
            else
            {
                answer = $"Bu durağımızda rahat ve ferah yazlık giysiler tercih edebilirsiniz. Gün içinde adım sayınız artacağından konforlu bir spor ayakkabı idealdir. Manzara çekimlerinde canlı ve kontrast renkler (beyaz, bej, kobalt mavisi) fotoğraflarda mükemmel görünecektir!";
            }
        }
        else if (qLower.Contains("rezervasyon") || qLower.Contains("bilet") || qLower.Contains("sıra") || qLower.Contains("önceden") || qLower.Contains("yer"))
        {
            answer = $"Bu durak için kesinlikle sıra bekleme endişeniz olmasın! {plan.Hotel.Name} misafirimiz olduğunuz için OtelPro VIP Concierge sistemimiz adınıza öncelikli masa ve giriş kaydını otomatik iletmektedir. Durağa varışınızdan önce kapıda isminizle karşılanacaksınız. Müze Kartınız varsa dijitalden okutabilirsiniz; bilet gerektiren duraklarda şoförümüz temini sağlamaktadır.";
        }
        else if (qLower.Contains("fotoğraf") || qLower.Contains("manzara") || qLower.Contains("foto") || qLower.Contains("çekim") || qLower.Contains("kadraj") || qLower.Contains("kare"))
        {
            answer = $"Bu durakta fotoğraf için en büyüleyici kadraj noktası: {stop.Specialty.Split('&').FirstOrDefault()?.Trim() ?? "Ana teras ve seyir platformu"}. En iyi ışık açısı saat {stop.TimeSlot.Split('-').FirstOrDefault()?.Trim() ?? "öğleden sonra"} saatlerindedir. Özellikle arka planda derin ufuk çizgisi veya tarihi taş doku görünürken geniş açı (0.5x) modunda portre çekimi yapmanızı kesinlikle tavsiye ederim!";
        }
        else if (qLower.Contains("çocuk") || qLower.Contains("bebek") || qLower.Contains("puset") || qLower.Contains("aile"))
        {
            if (stop.Category.Contains("Activity") && (stop.Title.Contains("Rafting") || stop.Title.Contains("ATV") || stop.Title.Contains("Zipline")))
            {
                answer = $"Bu aktivitemiz güvenlik gereği 7 yaş ve üzeri misafirlerimiz için uygundur. Daha küçük yaş grupları için tesis kenarındaki seyir çay bahçesinde animatör eşliğinde güvenli dinlenme alanı mevcuttur. Tesis alanına kadar bebek arabasıyla düz ayak erişim mümkündür.";
            }
            else
            {
                answer = $"Bu durak çocuklu ailelerimiz için son derece konforlu ve güvenlidir! Düz ayak erişim ve puset rampası bulunmaktadır. Ayrıca çocuk dostu menü ve mama sandalyesi mevcuttur. Otel misafiri ailelere personelimiz ekstra hassasiyet ve öncelik göstermektedir.";
            }
        }
        else if (qLower.Contains("alternatif") || qLower.Contains("yağmur") || qLower.Contains("değiştir") || qLower.Contains("hava") || qLower.Contains("farklı"))
        {
            answer = $"Eğer bu durağı değiştirmek veya hava şartları sebebiyle kapalı bir mekana geçmek isterseniz; rotanız üzerindeki en yakın B planımız 'Tarihi Taş Bedesten & Etnografya Müzesi' veya otelimizin panoramik SPA/ısıtmalı havuz alanıdır. Dilediğiniz anda transfer şoförünüze bildirmeniz durumunda rotanız hemen güncellenir.";
        }
        else if (qLower.Contains("indirim") || qLower.Contains("ikram") || qLower.Contains("ayrıcalık") || qLower.Contains("fiyat") || qLower.Contains("otel"))
        {
            answer = $"{plan.Hotel.Name} misafirlerimize bu durakta: {stop.ConciergeTip}. Ayrıca hesapta %10 OtelPro sadakat indirimi uygulanmakta ve şefin imza tatlısı veya karşılama kokteyli ikram edilmektedir. Rezervasyon kaydınız otel oda numaranızla doğrudan eşleştirilmiştir.";
        }
        else
        {
            answer = $"Çok güzel bir soru! {stop.Title} ({stop.CategoryName}), bölgenin en seçkin ve popüler duraklarından biridir. Buradaki en kritik deneyim: {stop.Specialty}. Benim bir bölge baş rehberi olarak tavsiyem; acele etmeden, {stop.ConciergeTip.ToLower()} anın tadını çıkarmanızdır. Yol boyunca aklınıza gelen her detay için bana dilediğiniz zaman ulaşabilirsiniz!";
        }

        return Task.FromResult(answer);
    }

    private TourGuideProfile GenerateTourGuideForCity(string city, string profile, Hotel hotel)
    {
        var guide = new TourGuideProfile();
        if (city.Contains("Bodrum") || city.Contains("Muğla") || city.Contains("Fethiye"))
        {
            guide.Name = "Kaan Demir";
            guide.Avatar = "🧑‍💼";
            guide.Title = "Ege Koyları & Akdeniz Baş Rehberi";
            guide.Badge = "TUREB Lisanslı AI Rehber";
            guide.Experience = "14 Yıl Ege & Akdeniz Uzmanlığı";
            guide.Rating = "4.99 / 5.0 (2.150+ Yorum)";
            guide.Languages = "Türkçe, English, Deutsch, Français";
            guide.PhoneDirect = "+90 (532) 410 88 00";
            guide.PersonalizedWelcome = $"Merhaba! {hotel.Name} tatiliniz boyunca kişisel tur rehberiniz olarak rotanızı adım adım takip ediyorum. Ege'nin turkuaz koyları, zeytinyağlıları ve gün batımı terasları için gün boyu yanınızdayım. Herhangi bir durakta 'Rehbere Danış' butonuna basarak aklınıza takılan her şeyi bana anında sorabilirsiniz!";
        }
        else if (city.Contains("Kapadokya") || city.Contains("Nevşehir"))
        {
            guide.Name = "Selin Kaya";
            guide.Avatar = "👩‍💼";
            guide.Title = "Kapadokya Vadileri & Arkeoloji Uzmanı";
            guide.Badge = "Arkeolog & Lisanslı Baş Rehber";
            guide.Experience = "16 Yıl Bölge Arkeoloji Deneyimi";
            guide.Rating = "4.98 / 5.0 (1.890+ Yorum)";
            guide.Languages = "Türkçe, English, Italiano, 日本語";
            guide.PhoneDirect = "+90 (533) 280 44 11";
            guide.PersonalizedWelcome = $"Kapadokya'nın masalsı peribacalarına hoş geldiniz! Bugün vadilerin gizli geçitlerinden Kızılçukur gün batımına uzanan kusursuz bir rotadayız. Her durak hakkında sorularınızı yanıtlamaktan mutluluk duyarım!";
        }
        else if (city.Contains("Mardin"))
        {
            guide.Name = "Şiyar Özmen";
            guide.Avatar = "🧔";
            guide.Title = "Mezopotamya Taş Mimarisi & Kültür Mirası Uzmanı";
            guide.Badge = "Kültür Bakanlığı Kıdemli Rehber";
            guide.Experience = "18 Yıl Eski Mardin Uzmanlığı";
            guide.Rating = "5.0 / 5.0 (2.400+ Yorum)";
            guide.Languages = "Türkçe, Süryanice, English, العربية";
            guide.PhoneDirect = "+90 (542) 710 33 22";
            guide.PersonalizedWelcome = $"Gündüzü seyran, gecesi gerdanlık Mardin'e hoş geldiniz. Binlerce yıllık taş konakların, manastırların ve eşsiz lezzetlerin tüm sırlarını bugün birlikte keşfedeceğiz.";
        }
        else if (city.Contains("İstanbul"))
        {
            guide.Name = "Emre Yıldız";
            guide.Avatar = "🏛️";
            guide.Title = "Tarihi Yarımada & Boğaziçi Kıdemli Rehberi";
            guide.Badge = "TUREB Kokartlı Baş Rehber";
            guide.Experience = "15 Yıl Saray & Boğaz Uzmanlığı";
            guide.Rating = "4.97 / 5.0 (3.100+ Yorum)";
            guide.Languages = "Türkçe, English, Deutsch, Español";
            guide.PhoneDirect = "+90 (532) 990 12 34";
            guide.PersonalizedWelcome = $"İki kıtanın kavuştuğu dünya başkenti İstanbul'daki rotanızda rehberinizim. Saray geçişlerinden gizli çatı teraslarına kadar aklınıza gelen her soruyu durak kartlarından bana iletebilirsiniz.";
        }
        else if (city.Contains("İzmir") || city.Contains("Çeşme") || city.Contains("Alaçatı"))
        {
            guide.Name = "Ece Arslan";
            guide.Avatar = "🍷";
            guide.Title = "Ege Bağ Yolu & Gastronomi Rota Tasarımcısı";
            guide.Badge = "Sommelier & Turizm Rehberi";
            guide.Experience = "11 Yıl Urla & Çeşme Deneyimi";
            guide.Rating = "4.99 / 5.0 (1.650+ Yorum)";
            guide.Languages = "Türkçe, English, Ελληνικά";
            guide.PhoneDirect = "+90 (535) 600 77 88";
            guide.PersonalizedWelcome = $"Ege'nin zeytin kokulu esintilerine hoş geldiniz! Urla bağ yollarından Alaçatı'nın begonvilli sokaklarına uzanan en seçkin lezzet duraklarında gün boyu yanınızdayım.";
        }
        else if (city.Contains("Dubai"))
        {
            guide.Name = "Leyla Mansur";
            guide.Avatar = "✨";
            guide.Title = "Körfez VIP Deneyimler & Çöl Safarisi Uzmanı";
            guide.Badge = "Dubai Tourism Board Certified";
            guide.Experience = "10 Yıl VIP Tur Yönetimi";
            guide.Rating = "4.96 / 5.0 (1.320+ Yorum)";
            guide.Languages = "Türkçe, English, العربية, Русский";
            guide.PhoneDirect = "+971 (50) 880 99 22";
            guide.PersonalizedWelcome = $"Dubai'nin fütüristik siluetine ve altın çöllerine hoş geldiniz. VIP geçişler, helikopter manzaraları ve lüks çöl vahaları rotanızda rehberlik için hazır bekliyorum.";
        }
        else
        {
            guide.Name = "Deniz Yılmaz";
            guide.Avatar = "🧑‍💼";
            guide.Title = "Akdeniz Sahilleri & Antik Kentler Uzmanı";
            guide.Badge = "TUREB Lisanslı AI Rehber";
            guide.Experience = "13 Yıl Bölge Deneyimi";
            guide.Rating = "4.98 / 5.0 (2.280+ Yorum)";
            guide.Languages = "Türkçe, English, Deutsch, Pусский";
            guide.PhoneDirect = "+90 (532) 550 66 77";
            guide.PersonalizedWelcome = $"Akdeniz'in masmavi kıyılarına hoş geldiniz! {hotel.Name} merkezli rotamızda antik kalıntılardan turkuaz koylara harika bir gün geçireceğiz.";
        }

        guide.Status = "🟢 Çevrimiçi · Rotanızı Anlık Takip Ediyor";
        return guide;
    }

    private WeatherInfo GenerateWeatherForCity(string city)
    {
        if (city.Contains("Antalya") || city.Contains("Bodrum") || city.Contains("Çeşme") || city.Contains("Fethiye"))
        {
            return new WeatherInfo(city, 26, "Ilık & Güneşli", "☀️", 48, "12 km/s B", 7, "Güneş kremi, şapka ve güneş gözlüğü almayı unutmayınız.");
        }
        if (city.Contains("Kapadokya") || city.Contains("Nevşehir"))
        {
            return new WeatherInfo(city, 19, "Açık & Berrak", "🎈", 35, "8 km/s K", 5, "Sabah balon saatinde hava serindir; hafif bir mont veya şal önerilir.");
        }
        if (city.Contains("Mardin"))
        {
            return new WeatherInfo(city, 23, "Güneşli & Kuru Esintili", "🏛️", 28, "14 km/s G", 6, "Tarihi taş merdivenler için rahat yürüyüş ayakkabısı şarttır.");
        }
        if (city.Contains("Uludağ") || city.Contains("Bursa"))
        {
            return new WeatherInfo(city, 14, "Zirvede Serin & Dağ Havası", "🌲", 60, "15 km/s KD", 4, "Rüzgarlık ve termal giysiler tavsiye edilir.");
        }

        // İstanbul
        return new WeatherInfo(city, 21, "Boğaz Esintili & Parçalı Bulutlu", "⛅", 55, "16 km/s KD", 5, "Boğaz tekne turunda rüzgar olabileceğinden ince hırka bulundurunuz.");
    }

    private List<string> GeneratePackingTips(string city, string profile)
    {
        var list = new List<string>
        {
            "👟 Rahat yürüyüş ayakkabısı (Günde ortalama 7.000 adım)",
            "🔋 Taşınabilir şarj cihazı (Powerbank)",
            "💳 Müze Kart / Dijital Kimlik Belgesi"
        };

        if (profile == "adventure")
        {
            list.Add("🧗 Rahat spor kıyafetleri & yedek tişört");
            list.Add("💧 Su geçirmez telefon kılıfı & GoPro aksiyon kamerası");
            list.Add("🥾 Kaymaz tabanlı trekking / outdoor ayakkabısı");
        }
        else if (profile == "romantic")
        {
            list.Add("👗 Şık akşam kıyafeti & parfümeri");
            list.Add("🕶️ Gün batımı için şık güneş gözlüğü");
            list.Add("🧣 Akşam deniz esintisi için hafif şal");
        }
        else if (profile == "history")
        {
            list.Add("🧢 Güneş şapkası & müze rehberi");
            list.Add("📓 Not defteri veya ses kayıt cihazı");
            list.Add("🧦 Tarihi mekan ve ibadethane ziyaretleri için çorap");
        }
        else if (profile == "family")
        {
            list.Add("🧴 Çocuk güneş kremi & ıslak mendil");
            list.Add("🧸 Küçük oyuncak & atıştırmalık kutusu");
            list.Add("🩱 Çocuk mayosu & kolluk");
        }
        else if (profile == "wellness")
        {
            list.Add("🧘 Rahat yoga taytı & pamuklu giysiler");
            list.Add("🌿 Şahsi bakım kremleri & su matarası");
            list.Add("🩴 Kaymaz banyo terliği");
        }
        else if (profile == "gourmet")
        {
            list.Add("👔 Şık akşam yemeği kıyafeti (Smart Casual)");
            list.Add("🍷 Gurme not defteri & tadım günlüğü");
        }

        if (city.Contains("Bodrum") || city.Contains("Fethiye") || city.Contains("Antalya") || city.Contains("Çeşme"))
        {
            list.Add("🩱 Mayo & Havlu (Gizli koylarda yüzme molası)");
        }
        else if (city.Contains("Kapadokya"))
        {
            list.Add("🧥 Sabah gün doğumu balon seyri için hırka / rüzgarlık");
        }
        else if (city.Contains("Mardin"))
        {
            list.Add("🧣 Manastır ve medrese ziyaretleri için örtü");
        }

        return list;
    }

    private List<ItineraryStop> GenerateStopsForCityAndProfile(Hotel hotel, string city, string profile, int days, string pace, string budget)
    {
        decimal budgetMult = budget == "luxury" ? 1.6m : (budget == "local" ? 0.75m : 1.0m);
        double hLat = hotel.Latitude ?? 36.8550;
        double hLng = hotel.Longitude ?? 30.7950;

        return profile switch
        {
            "adventure" => GenerateAdventureStops(hotel, city, days, budgetMult, hLat, hLng),
            "history" => GenerateHistoryStops(hotel, city, days, budgetMult, hLat, hLng),
            "family" => GenerateFamilyStops(hotel, city, days, budgetMult, hLat, hLng),
            "gourmet" => GenerateGourmetStops(hotel, city, days, budgetMult, hLat, hLng),
            "wellness" => GenerateWellnessStops(hotel, city, days, budgetMult, hLat, hLng),
            _ => GenerateRomanticStops(hotel, city, days, budgetMult, hLat, hLng)
        };
    }

    // ==========================================
    // 🏄‍♂️ 1. MACERA & ADRENALİN ROTASI
    // ==========================================
    private List<ItineraryStop> GenerateAdventureStops(Hotel hotel, string city, int days, decimal mult, double hLat, double hLng)
    {
        var list = new List<ItineraryStop>();

        // 1. GÜN (ADVENTURE)
        list.Add(new ItineraryStop
        {
            Id = 1, Day = 1, TimeSlot = "08:30 - 10:00",
            Title = "Sporcu & Enerji Kahvaltısı & Güvenlik Brifingi",
            Category = "Breakfast", CategoryName = "Yüksek Proteinli Kahvaltı", CategoryIcon = "🍳",
            Description = "Fındıklı yulaf kasesi, organik çiftlik yumurtası, muzlu protein smoothie ve dağ balı ile günün ekstrem aktivitelerine hazır başlangıç.",
            Specialty = "Fındık Ezmeli Protein Kasesi & Taze Sıkılmış Greyfurt Suyu",
            ConciergeTip = "Zorlu parkur öncesi bol su içiniz; rehberlerimiz enerji barları ve elektrolit içeceklerini dağıtacaktır.",
            DistanceText = "Otel İçi Teras", TransitText = "0 dk", EstCost = Math.Round(350m * mult, 0),
            Latitude = hLat, Longitude = hLng, ImageUrl = "/images/aegean_dining.jpg",
            Address = hotel.Address ?? "Otel Spor Terası", Rating = 4.9m, BadgeColor = "#f59e0b"
        });

        list.Add(new ItineraryStop
        {
            Id = 2, Day = 1, TimeSlot = "10:30 - 13:30",
            Title = city.Contains("Kapadokya") 
                ? "Kılıçlar & Güllüdere Vadisi Ekstrem ATV Safarisi" 
                : (city.Contains("Fethiye") ? "Saklıkent Kanyonu Azgın Nehir Raftingi" : "Köprülü Kanyon Çılgın Rafting & Kanyon İçi Zipline"),
            Category = "Extreme", CategoryName = "Ekstrem Macera & Nehir Geçişi", CategoryIcon = "🚣",
            Description = "14 kilometrelik azgın debili nehir boyunca profesyonel lisanslı rehberlerle bot idaresi ve 600 metrelik çelik hat üzerinden kanyon üstü zipline uçuşu.",
            Specialty = "3. Derece Akıntı Geçişi & 60 Metre Kanyon Zipline",
            ConciergeTip = "Kask kameraları ve su geçirmez aksiyon kılıfları rehber ekibimizce ücretsiz sağlanmaktadır.",
            DistanceText = "Otele 18 km", TransitText = "4x4 Transferle 22 dk", EstCost = Math.Round(1200m * mult, 0),
            Latitude = hLat + 0.045, Longitude = hLng + 0.035, ImageUrl = "/images/duden_waterfall.jpg",
            Address = "Köprülü / Kanyon Macera Parkuru", Rating = 5.0m, BadgeColor = "#0284c7"
        });

        list.Add(new ItineraryStop
        {
            Id = 3, Day = 1, TimeSlot = "13:45 - 15:00",
            Title = "Nehir Kıyısı Asma İskelede Közde Alabalık & Enerji Menüsü",
            Category = "Lunch", CategoryName = "Nehir Kenarı Barbekü", CategoryIcon = "🍽️",
            Description = "Çağlayan su sesleri altında közde odun ateşinde pişmiş taze alabalık, fırın lavaş ve buz gibi yayık ayranı.",
            Specialty = "Odun Ateşinde Alabalık & Dağ Kekiği Salatası",
            ConciergeTip = "Ayaklarınızı buz gibi nehir suyuna sokabileceğiniz özel sedir masalar ayrılmıştır.",
            DistanceText = "Otele 18.5 km", TransitText = "Yürüyerek 2 dk", EstCost = Math.Round(550m * mult, 0),
            Latitude = hLat + 0.047, Longitude = hLng + 0.036, ImageUrl = "/images/aegean_dining.jpg",
            Address = "Kanyon İçi Nehir Tesisleri", Rating = 4.8m, BadgeColor = "#10b981"
        });

        list.Add(new ItineraryStop
        {
            Id = 4, Day = 1, TimeSlot = "15:30 - 18:00",
            Title = city.Contains("Kapadokya") 
                ? "Aşk Vadisi & Peri Bacaları Çamur ATV Rallisi" 
                : "Toros Dağları Çamur & Toz Off-Road Dağ ATV Safarisi",
            Category = "Extreme", CategoryName = "Off-Road 4x4 ATV Safarisi", CategoryIcon = "🏎️",
            Description = "Kayalık dağ yolları, çamur hendekleri ve dik sırt tırmanışlarıyla dolu 28 kilometrelik motorize heyecan fırtınası.",
            Specialty = "Dik Tepe Tırmanışı & Çamur Parkuru Geçişi",
            ConciergeTip = "Gözlük ve kask takmak zorunludur; tozdan koruyan özel OtelPro bandana hediye edilir.",
            DistanceText = "Otele 8.5 km", TransitText = "Transferle 12 dk", EstCost = Math.Round(950m * mult, 0),
            Latitude = hLat - 0.025, Longitude = hLng + 0.030, ImageUrl = "/images/atv_safari.jpg",
            Address = "Dağ Etekleri ATV Safari İstasyonu", Rating = 4.9m, BadgeColor = "#8b5cf6"
        });

        list.Add(new ItineraryStop
        {
            Id = 5, Day = 1, TimeSlot = "18:30 - 20:00",
            Title = city.Contains("Kapadokya")
                ? "Kızılçukur Vadisi Zirvesinde Gün Batımı & Kaya İnişi"
                : "Rüzgar Sörfü (Windsurf / Kitesurf) & Gün Batımı Hız Denemesi",
            Category = "Sport", CategoryName = "Rüzgar Sörfü & Adrenalin", CategoryIcon = "🏄‍♂️",
            Description = "Akşamüstü çıkan sert termik rüzgar eşliğinde dalgalar üzerinde hız rekoru denemesi ve gün batımı sörfü.",
            Specialty = "15 Knot Rüzgarla Planing & Slalom Dönüşü",
            ConciergeTip = "Eğitmenler telsiz kulaklık ile su üstünde anlık rota yönlendirmesi yapar.",
            DistanceText = "Otele 4.2 km", TransitText = "Taksiyle 7 dk", EstCost = Math.Round(750m * mult, 0),
            Latitude = hLat + 0.015, Longitude = hLng - 0.025, ImageUrl = "/images/windsurf_cesme.jpg",
            Address = "Rüzgar Sörfü & Su Sporları Kulübü", Rating = 4.9m, BadgeColor = "#ec4899"
        });

        list.Add(new ItineraryStop
        {
            Id = 6, Day = 1, TimeSlot = "20:30 - 23:30",
            Title = "Sahil Kamp Ateşi Başında Barbekü & Canlı Rock Müzik",
            Category = "Night", CategoryName = "Ateş Başı Barbekü & Canlı Müzik", CategoryIcon = "🔥",
            Description = "Sahilde dev kamp ateşi etrafında dinlendirici bir atmosfer; kalın dilim dana antrikot, köz sebzeler, soğuk fıçı bira ve canlı rock performansı.",
            Specialty = "Közde Angus Burger & Ateşte Çıtır Marshmallow",
            ConciergeTip = "Günün aksiyon videoları ateş başındaki projeksiyon perdesine yansıtılır.",
            DistanceText = "Otele 1.5 km", TransitText = "Yürüyerek 10 dk", EstCost = Math.Round(950m * mult, 0),
            Latitude = hLat + 0.008, Longitude = hLng - 0.012, ImageUrl = "/images/sunset_bar.jpg",
            Address = "Sahil Kampı & Canlı Sahne", Rating = 5.0m, BadgeColor = "#f59e0b"
        });

        // 2. GÜN (ADVENTURE)
        if (days >= 2)
        {
            list.Add(new ItineraryStop
            {
                Id = 7, Day = 2, TimeSlot = "09:00 - 12:30",
                Title = city.Contains("Kapadokya") 
                    ? "Gün Doğumu Sıcak Hava Balon Uçuşu & Vadi Seyri" 
                    : (city.Contains("Fethiye") ? "Babadağ 1965m Ölüdeniz Tandem Yamaç Paraşütü" : "Tahtalı Dağı 2365m Zirveden Tandem Yamaç Paraşütü"),
                Category = "Extreme", CategoryName = "Yamaç Paraşütü (Paragliding)", CategoryIcon = "🪂",
                Description = "Bulutların arasından 2000+ metre yükseklikten göğe havalanıp turkuaz suların ve koyların üzerinden 40 dakika süren nefes kesici süzülüş.",
                Specialty = "Spiral Dalış, 360 Derece Termik Dönüş & 4K Uçuş Kaydı",
                ConciergeTip = "Pilotunuz 360 derece aksiyon kamerası ile uçuşunuzu kaydeder; telefonunuza anında aktarılır.",
                DistanceText = "Otele 24 km", TransitText = "Teleferik / Dağ Transferi", EstCost = Math.Round(3200m * mult, 0),
                Latitude = hLat + 0.070, Longitude = hLng - 0.040, ImageUrl = "/images/fethiye_lagoon.jpg",
                Address = "Zirve Yamaç Paraşütü Pisti", Rating = 5.0m, BadgeColor = "#38bdf8"
            });

            list.Add(new ItineraryStop
            {
                Id = 8, Day = 2, TimeSlot = "13:00 - 14:30",
                Title = "Dağ Evi Taş Fırın Güveç & Köy Ekmeği Sofrası",
                Category = "Lunch", CategoryName = "Dağ Evi Öğle Yemeği", CategoryIcon = "🍽️",
                Description = "İniş sonrası orman içi taş kulübede fırından yeni çıkmış kuzu etli güveç, köy tereyağı ve çıtır tırnak pide.",
                Specialty = "Taş Fırın Kuzu Güveç & Çömlek Yoğurdu",
                ConciergeTip = "Yayla havasında yorgunluk kahvesi közde pişirilir.",
                DistanceText = "İniş Alanında", TransitText = "Yürüyerek 3 dk", EstCost = Math.Round(600m * mult, 0),
                Latitude = hLat + 0.035, Longitude = hLng - 0.020, ImageUrl = "/images/aegean_dining.jpg",
                Address = "Yayla Evi Doğal Restoranı", Rating = 4.8m, BadgeColor = "#10b981"
            });

            list.Add(new ItineraryStop
            {
                Id = 9, Day = 2, TimeSlot = "15:00 - 18:00",
                Title = city.Contains("Kapadokya") 
                    ? "Derinkuyu 8 Katlı Yeraltı Şehri Tünel Keşfi" 
                    : "Açık Denizde Tüplü Dalış (Scuba Diving) & Batık Gemi Keşfi",
                Category = "Extreme", CategoryName = "Tüplü Dalış & Sualtı Mağarası", CategoryIcon = "🤿",
                Description = "18 metre derinlikteki tarihi batık gemi ve sualtı kanyonunda Caretta Caretta kaplumbağalarıyla birlikte tüplü dalış.",
                Specialty = "Batık Gemi Gövdesi İçi Dalış & Sualtı Fotoğrafçılığı",
                ConciergeTip = "CMAS / PADI sertifikalı dalgıç eğitmenleri birebir eşlik eder.",
                DistanceText = "Otele 3.8 km", TransitText = "Dalış Teknesiyle", EstCost = Math.Round(1400m * mult, 0),
                Latitude = hLat - 0.015, Longitude = hLng - 0.030, ImageUrl = "/images/scuba_diving.jpg",
                Address = "Marina Dalış Merkezi İskelesi", Rating = 5.0m, BadgeColor = "#06b6d4"
            });

            list.Add(new ItineraryStop
            {
                Id = 10, Day = 2, TimeSlot = "19:30 - 22:30",
                Title = "Sürat Teknesi (Speedboat) Mehtap Safarisi & Sahil Lounge Partisi",
                Category = "Night", CategoryName = "Gece Sürat Teknesi Safarisi", CategoryIcon = "⚡",
                Description = "50 knot süratli motorlarla ay ışığı altında koylar arası gece safarisi ve dönüşte sahil barında DJ performansı.",
                Specialty = "Açık Deniz Gece Hız Turu & İmza Enerji Kokteyli",
                ConciergeTip = "Deniz rüzgarına karşı hafif bir rüzgarlık mont giyilmesi tavsiye edilir.",
                DistanceText = "Otele 2.0 km", TransitText = "Taksiyle 5 dk", EstCost = Math.Round(1100m * mult, 0),
                Latitude = hLat + 0.012, Longitude = hLng - 0.015, ImageUrl = "/images/boat_tour.jpg",
                Address = "Marina VIP Hız İskelesi", Rating = 4.9m, BadgeColor = "#8b5cf6"
            });
        }

        // 3. GÜN (ADVENTURE)
        if (days >= 3)
        {
            list.Add(new ItineraryStop
            {
                Id = 11, Day = 3, TimeSlot = "09:30 - 13:00",
                Title = "Kanyon Geçişi (Canyoning) & 12 Metreden Şelale İnişi (Abseiling)",
                Category = "Extreme", CategoryName = "Kanyoning & Kaya Tırmanışı", CategoryIcon = "🧗",
                Description = "Neopren kıyafetler ve kasklarla dar kanyon sularında yüzüş, doğal kaya kaydıraklarından atlayış ve ipli dikey şelale inişi.",
                Specialty = "Doğal Su Kaydırağı & 12 Metre Şelale Abseiling",
                ConciergeTip = "Tüm tırmanış emniyet kemerleri uluslararası UIAA sertifikalıdır.",
                DistanceText = "Otele 15 km", TransitText = "Transferle 18 dk", EstCost = Math.Round(1350m * mult, 0),
                Latitude = hLat + 0.050, Longitude = hLng + 0.020, ImageUrl = "/images/duden_waterfall.jpg",
                Address = "Kanyon Canyoning Başlangıç Üssü", Rating = 5.0m, BadgeColor = "#0284c7"
            });

            list.Add(new ItineraryStop
            {
                Id = 12, Day = 3, TimeSlot = "13:30 - 15:00",
                Title = "Kanyon Çıkışı Çoban Kavurma & Köz Biber Şöleni",
                Category = "Lunch", CategoryName = "Kapanış Öğle Yemeği", CategoryIcon = "🍽️",
                Description = "Zorlu kanyon geçişi sonrası sac tavada tereyağlı kuzu kavurma, köy ekmeği ve közlenmiş patlıcan salatası.",
                Specialty = "Sac Tava Çoban Kavurma & Közlenmiş Köy Biberi",
                ConciergeTip = "Ekstra enerji için fırın sıcak tahin helvası ikram edilir.",
                DistanceText = "Kanyon Çıkışı", TransitText = "0 dk", EstCost = Math.Round(650m * mult, 0),
                Latitude = hLat + 0.052, Longitude = hLng + 0.021, ImageUrl = "/images/aegean_dining.jpg",
                Address = "Kanyon Değirmen Restoran", Rating = 4.8m, BadgeColor = "#10b981"
            });

            list.Add(new ItineraryStop
            {
                Id = 13, Day = 3, TimeSlot = "15:30 - 18:30",
                Title = "Off-Road Buggy Rallisi & Ekstrem Sporcu Madalya Töreni",
                Category = "Activity", CategoryName = "Buggy Rallisi & Kapanış", CategoryIcon = "🏆",
                Description = "Özel yarış pistinde zamana karşı buggy mücadelesi ve turun sonunda şampanya patlatılarak takdim edilen OtelPro Ekstrem Gezgin Madalyası.",
                Specialty = "Zamana Karşı Buggy Etabı & Hatıra Madalyası",
                ConciergeTip = "Otel transfer aracınız bagajlarınızla birlikte pist bitişinde sizi bekler.",
                DistanceText = "Otele 10 km", TransitText = "VIP Transfer", EstCost = Math.Round(850m * mult, 0),
                Latitude = hLat + 0.020, Longitude = hLng + 0.015, ImageUrl = "/images/atv_safari.jpg",
                Address = "Motor Sporları Pisti & Kulübü", Rating = 5.0m, BadgeColor = "#f59e0b"
            });
        }

        return list;
    }

    // ==========================================
    // 💑 2. ROMANTİK ÇİFT & BALAYI ROTASI
    // ==========================================
    private List<ItineraryStop> GenerateRomanticStops(Hotel hotel, string city, int days, decimal mult, double hLat, double hLng)
    {
        var list = new List<ItineraryStop>();

        list.Add(new ItineraryStop
        {
            Id = 1, Day = 1, TimeSlot = "09:30 - 11:00",
            Title = "Çiçek Kokulu Taş Avluda Çiftlere Özel Serpme Ege Kahvaltısı",
            Category = "Breakfast", CategoryName = "Romantik Avlu Kahvaltısı", CategoryIcon = "🍳",
            Description = "Zakkum ve begonviller arasında, iki kişilik özel ahşap masada ev yapımı reçeller, taze kruvasanlar ve köpüklü şampanya eşliğinde huzurlu başlangıç.",
            Specialty = "Lavanta Reçeli, Sıcak Pişi & Çilekli Şampanya",
            ConciergeTip = "En sessiz ve manzaralı köşe masa çiftiniz adına rezerve edilmiştir.",
            DistanceText = "Otel Avlusu", TransitText = "0 dk", EstCost = Math.Round(450m * mult, 0),
            Latitude = hLat, Longitude = hLng, ImageUrl = "/images/aegean_dining.jpg",
            Address = hotel.Address ?? "Otel Taş Avlu", Rating = 4.9m, BadgeColor = "#ec4899"
        });

        list.Add(new ItineraryStop
        {
            Id = 2, Day = 1, TimeSlot = "11:30 - 14:30",
            Title = city.Contains("Kapadokya") 
                ? "Aşk Vadisi Peri Bacaları Arasında Fayton & Fotoğraf Turu" 
                : "Özel VIP Motoryat ile Sakin Koylarda Baş Başa Gezinti",
            Category = "Cruise", CategoryName = "Özel Yat & Deniz Seyri", CategoryIcon = "🛥️",
            Description = "Yalnızca çifte tahsis edilen lüks motoryat ile kalabalıktan uzak turkuaz bir koya demirleme, berrak sularda yüzme ve güneşlenme.",
            Specialty = "Gizli Koylarda Yüzme Molası & Taze Meyve İkramı",
            ConciergeTip = "Kaptandan rica edilerek denizin ortasında baş başa müzik yayını açtırılabilir.",
            DistanceText = "Otele 2.5 km", TransitText = "VIP Transferle 5 dk", EstCost = Math.Round(2200m * mult, 0),
            Latitude = hLat + 0.020, Longitude = hLng - 0.020, ImageUrl = "/images/boat_tour.jpg",
            Address = "Marina Özel İskele", Rating = 5.0m, BadgeColor = "#38bdf8"
        });

        list.Add(new ItineraryStop
        {
            Id = 3, Day = 1, TimeSlot = "14:45 - 16:30",
            Title = "Kumsal Terasında Deniz Mahsulleri & Beyaz Şarap",
            Category = "Lunch", CategoryName = "Romantik Öğle Yemeği", CategoryIcon = "🍽️",
            Description = "Tül perdelerin hafif esintiyle dalgalandığı sahil gazebosunda ızgara deniz tarağı, levrek marin ve soğuk beyaz şarap.",
            Specialty = "Fesleğenli Levrek Marin & Deniz Mahsullü Linguine",
            ConciergeTip = "Yemek sonrası gazeboda soğuk havlu servisi yapılır.",
            DistanceText = "Sahil Şeridi", TransitText = "Yürüyerek 2 dk", EstCost = Math.Round(850m * mult, 0),
            Latitude = hLat + 0.015, Longitude = hLng - 0.018, ImageUrl = "/images/aegean_dining.jpg",
            Address = "Sahil Gazebo Restaurant", Rating = 4.9m, BadgeColor = "#10b981"
        });

        list.Add(new ItineraryStop
        {
            Id = 4, Day = 1, TimeSlot = "17:00 - 18:30",
            Title = "Otel Spa Süitinde Çift Masajı & Jakuzi Ritüeli",
            Category = "Spa", CategoryName = "Çiftlere Özel Masaj", CategoryIcon = "💆",
            Description = "Gül yaprakları serpiştirilmiş jakuzi, adaçayı aromalı yağlar ile 60 dakikalık senkronize Bali masajı.",
            Specialty = "Sıcak Taş Masajı & Gül Suyu Jakuzi Seansı",
            ConciergeTip = "Masaj sonrası dinlenme odasında taze çilek ve çikolata ikram edilir.",
            DistanceText = "Otel İçi Spa", TransitText = "0 dk", EstCost = Math.Round(1600m * mult, 0),
            Latitude = hLat, Longitude = hLng, ImageUrl = "/images/room_suite.jpg",
            Address = "Otel Spa & Wellness Katı", Rating = 5.0m, BadgeColor = "#8b5cf6"
        });

        list.Add(new ItineraryStop
        {
            Id = 5, Day = 1, TimeSlot = "19:00 - 20:30",
            Title = "Falez Seyir Terasında Gün Batımı & Çilekli Bellini",
            Category = "Sunset", CategoryName = "Gün Batımı Kokteyli", CategoryIcon = "🌅",
            Description = "Güneşin denize kavuştuğu altın saatlerde iki kadeh Prosecco Bellini eşliğinde romantik sohbet ve büyüleyici manzara.",
            Specialty = "Taze Çilekli Bellini & Gurme Peynir Tahtası",
            ConciergeTip = "Saat tam 19:40'ta gün batımına karşı çift fotoğrafınızı otel fotoğrafçımız çeker.",
            DistanceText = "Otele 1.8 km", TransitText = "Taksiyle 5 dk", EstCost = Math.Round(550m * mult, 0),
            Latitude = hLat + 0.008, Longitude = hLng - 0.010, ImageUrl = "/images/sunset_bar.jpg",
            Address = "Seyir Tepesi Gün Batımı Terası", Rating = 4.9m, BadgeColor = "#ec4899"
        });

        list.Add(new ItineraryStop
        {
            Id = 6, Day = 1, TimeSlot = "20:45 - 23:30",
            Title = "Mum Işığında Keman Dinletisi Eşliğinde Şefin Akşam Yemeği",
            Category = "Dinner", CategoryName = "Mum Işığında Fine Dining", CategoryIcon = "🍷",
            Description = "Masada kırmızı güller, zarif mum ışığı, arka planda canlı keman ezgileri ve Michelin yıldızı konseptli 5 aşamalı menü.",
            Specialty = "Trüflü Risotto, Ağır Pişmiş Dana Yanağı & Lavantalı Sufle",
            ConciergeTip = "Masaya özel kutlama çiçeği ve çikolata tabağı otelimizce hediye olarak hazırlanır.",
            DistanceText = "Otele 3.2 km", TransitText = "VIP Transferle 7 dk", EstCost = Math.Round(2100m * mult, 0),
            Latitude = hLat + 0.020, Longitude = hLng - 0.030, ImageUrl = "/images/aegean_dining.jpg",
            Address = "Marina Fine Dining Rıhtımı", Rating = 5.0m, BadgeColor = "#ec4899"
        });

        if (days >= 2)
        {
            list.Add(new ItineraryStop
            {
                Id = 7, Day = 2, TimeSlot = "10:00 - 13:00",
                Title = "Nostaljik Cabriolet Araba ile Antik Sahil Yolu Gezisi",
                Category = "Activity", CategoryName = "Klasik Araba Gezisi", CategoryIcon = "🚗",
                Description = "Üstü açık klasik cabrio araç ile deniz kıyısı virajlı yollarda rüzgarı hissederek romantik keşif sürüşü.",
                Specialty = "Panoramik Sahil Virajları & Fotoğraf Molaları",
                ConciergeTip = "Fotoğraf durakları GPS haritasına önceden yüklenmiş olarak teslim edilir.",
                DistanceText = "Otel Kapısından Başlangıç", TransitText = "0 dk", EstCost = Math.Round(1800m * mult, 0),
                Latitude = hLat + 0.025, Longitude = hLng - 0.035, ImageUrl = "/images/room_seaview.jpg",
                Address = "Sahil Manzara Karayolu", Rating = 4.9m, BadgeColor = "#38bdf8"
            });

            list.Add(new ItineraryStop
            {
                Id = 8, Day = 2, TimeSlot = "13:30 - 15:30",
                Title = "Butik Şarap Bağı & Mahzeninde Çiftlere Özel Tadım",
                Category = "Lunch", CategoryName = "Şarap Tadımı & Gurme Öğle", CategoryIcon = "🍇",
                Description = "Yemyeşil asmalar arasında sommelier eşliğinde 6 kadeh ödüllü şarap tadımı ve yöresel peynir eşleşmesi.",
                Specialty = "Meşe Fıçıda Dinlenmiş Rezerve Şarap & Keçi Peynirleri",
                ConciergeTip = "Beğendiğiniz şarap şişesine adınızın yazılı olduğu özel etiket basılır.",
                DistanceText = "Otele 12 km", TransitText = "VIP Transferle 15 dk", EstCost = Math.Round(1200m * mult, 0),
                Latitude = hLat - 0.035, Longitude = hLng + 0.040, ImageUrl = "/images/aegean_dining.jpg",
                Address = "Bağ Evi ve Mahzeni", Rating = 5.0m, BadgeColor = "#8b5cf6"
            });

            list.Add(new ItineraryStop
            {
                Id = 9, Day = 2, TimeSlot = "19:30 - 23:00",
                Title = "Deniz Üstü İskelede Dalga Sesleri Eşliğinde Veda Yemeği",
                Category = "Dinner", CategoryName = "İskele Akşam Yemeği", CategoryIcon = "🕯️",
                Description = "Tamamen deniz üzerine uzanan ahşap iskelede meşaleler ve fenerler eşliğinde unutulmaz gurme lezzetler.",
                Specialty = "Kaya Levreği Izgara & Sıcak Çikolatalı Fondü",
                ConciergeTip = "Yemek bitiminde gökyüzüne dilek feneri bırakma seremonisi yapılır.",
                DistanceText = "Otele 2.4 km", TransitText = "Transferle 6 dk", EstCost = Math.Round(1900m * mult, 0),
                Latitude = hLat + 0.015, Longitude = hLng - 0.020, ImageUrl = "/images/sunset_bar.jpg",
                Address = "Özel İskele Restaurant", Rating = 5.0m, BadgeColor = "#ec4899"
            });
        }

        if (days >= 3)
        {
            list.Add(new ItineraryStop
            {
                Id = 10, Day = 3, TimeSlot = "10:30 - 13:30",
                Title = "Tarihi Taş Sokaklarda Butik Sanat & Mücevherat Gezisi",
                Category = "Activity", CategoryName = "Kültür & Romantik Alışveriş", CategoryIcon = "🛍️",
                Description = "El yapımı özel takılar, tasarım parfümler ve seramik atölyeleri arasında romantik hatıra alışverişi.",
                Specialty = "Kişiye Özel Tasarım Gümüş Takı & El Yapımı Seramik",
                ConciergeTip = "Otel misafirlerine anlaşmalı butiklerde %15 indirim sağlanır.",
                DistanceText = "Otele 1.5 km", TransitText = "Yürüyerek 10 dk", EstCost = Math.Round(600m * mult, 0),
                Latitude = hLat + 0.005, Longitude = hLng - 0.008, ImageUrl = "/images/historical_monument.jpg",
                Address = "Tarihi Çarşı & Sanat Sokağı", Rating = 4.8m, BadgeColor = "#0284c7"
            });

            list.Add(new ItineraryStop
            {
                Id = 11, Day = 3, TimeSlot = "14:00 - 16:30",
                Title = "Panoramik Tepede Veda Şampanyası & VIP Uğurlama",
                Category = "Departure", CategoryName = "Romantik Veda & Transfer", CategoryIcon = "🛫",
                Description = "Şehri kuşbakışı gören tepede veda kadehi ve limuzin transfer ile havalimanına konforlu yolculuk.",
                Specialty = "Çilekli Şampanya & Makaron Kutusu",
                ConciergeTip = "Tüm valizleriniz otelden doğrudan VIP araca yerleştirilmiştir.",
                DistanceText = "Otel Lobi", TransitText = "0 dk", EstCost = Math.Round(800m * mult, 0),
                Latitude = hLat, Longitude = hLng, ImageUrl = "/images/room_suite.jpg",
                Address = hotel.Address ?? "Otel Lobi", Rating = 5.0m, BadgeColor = "#38bdf8"
            });
        }

        return list;
    }

    // ==========================================
    // 🏛️ 3. TARİH & KÜLTÜR KAŞİFİ ROTASI
    // ==========================================
    private List<ItineraryStop> GenerateHistoryStops(Hotel hotel, string city, int days, decimal mult, double hLat, double hLng)
    {
        var list = new List<ItineraryStop>();

        list.Add(new ItineraryStop
        {
            Id = 1, Day = 1, TimeSlot = "09:00 - 10:30",
            Title = "Tarihi Kervansaray / Konak Avlusunda Geleneksel Kahvaltı",
            Category = "Breakfast", CategoryName = "Tarihi Konak Kahvaltısı", CategoryIcon = "🍳",
            Description = "Asırlık taş duvarlar arasında tulum peyniri, ev yapımı turunçgil reçelleri, taş fırın ekmekleri ve semaver çayı.",
            Specialty = "Köy Fırını Pişisi & Çömlek Peyniri",
            ConciergeTip = "Avlu içindeki tarihi kitabeleri ve su kuyusunu inceleyebilirsiniz.",
            DistanceText = "Otele 1.8 km", TransitText = "Yürüyerek 12 dk", EstCost = Math.Round(320m * mult, 0),
            Latitude = hLat + 0.008, Longitude = hLng - 0.010, ImageUrl = "/images/historical_monument.jpg",
            Address = "Tarihi Han Avlusu", Rating = 4.8m, BadgeColor = "#f59e0b"
        });

        list.Add(new ItineraryStop
        {
            Id = 2, Day = 1, TimeSlot = "11:00 - 13:30",
            Title = city.Contains("Kapadokya") 
                ? "Göreme Açık Hava Müzesi & Kaya Kiliseleri Keşfi" 
                : (city.Contains("Mardin") ? "Deyrulzafaran Manastırı & Antik Güneş Tapınağı" : "Hadrian Kapısı, Antik Surlar & Hıdırlık Kulesi"),
            Category = "Culture", CategoryName = "Antik Miras & Arkeoloji Parkı", CategoryIcon = "🏛️",
            Description = "Binlerce yıllık mermer sütunlar, imparatorluk zafer takları ve surlar boyu profesyonel arkeolog rehber eşliğinde anlatım.",
            Specialty = "Roma Mermer İşçiliği & Antik Sur Duvarları",
            ConciergeTip = "Müze kart geçerlidir; rehberimiz kulaklık sistemiyle detaylı tarih aktarımı yapar.",
            DistanceText = "Otele 2.4 km", TransitText = "Rehber Eşliğinde Yürüyüş", EstCost = Math.Round(250m * mult, 0),
            Latitude = hLat + 0.015, Longitude = hLng - 0.018, ImageUrl = "/images/historical_monument.jpg",
            Address = "Arkeoloji Koruma Alanı", Rating = 5.0m, BadgeColor = "#0284c7"
        });

        list.Add(new ItineraryStop
        {
            Id = 3, Day = 1, TimeSlot = "13:45 - 15:15",
            Title = "100 Yıllık Tarihi Lokantada Osmanlı Saray Mutfağı",
            Category = "Lunch", CategoryName = "Tarihi Saray Lezzetleri", CategoryIcon = "🍽️",
            Description = "Tarihi ahşap konakta bademli pirinçli kuzu incik, hünkarbeğendi ve demirhindi şerbeti.",
            Specialty = "Hünkarbeğendi & Soğuk Demirhindi Şerbeti",
            ConciergeTip = "Yemek öncesi şerbet sunumu tarihi gümüş bardaklarda yapılır.",
            DistanceText = "Otele 1.2 km", TransitText = "Yürüyerek 8 dk", EstCost = Math.Round(650m * mult, 0),
            Latitude = hLat + 0.010, Longitude = hLng - 0.012, ImageUrl = "/images/aegean_dining.jpg",
            Address = "Tarihi Çarşı İçi No:12", Rating = 4.9m, BadgeColor = "#10b981"
        });

        list.Add(new ItineraryStop
        {
            Id = 4, Day = 1, TimeSlot = "15:45 - 18:00",
            Title = "Bölge Arkeoloji Müzesi & İmparatorluk Heykelleri Salonu",
            Category = "Museum", CategoryName = "Küratörlü Müze Gezisi", CategoryIcon = "🏺",
            Description = "Dünya çapında ödüllü lahitler, altın sikkeler ve tanrı heykellerinin sergilendiği salonda özel küratör turu.",
            Specialty = "Herakles Lahdi & Roma İmparatorluk Heykelleri",
            ConciergeTip = "Fotoğraf çekerken flaş kapatılması eserlerin korunması için zorunludur.",
            DistanceText = "Otele 3.5 km", TransitText = "Taksiyle 8 dk", EstCost = Math.Round(200m * mult, 0),
            Latitude = hLat + 0.022, Longitude = hLng - 0.025, ImageUrl = "/images/historical_monument.jpg",
            Address = "Devlet Arkeoloji Müzesi", Rating = 5.0m, BadgeColor = "#8b5cf6"
        });

        list.Add(new ItineraryStop
        {
            Id = 5, Day = 1, TimeSlot = "18:30 - 20:00",
            Title = "Tarihi Saat Kulesi & Kale Burçlarında Gün Batımı Seyri",
            Category = "Sunset", CategoryName = "Tarihi Burç Manzarası", CategoryIcon = "🌅",
            Description = "Taş burçların üzerinden antik limana ve batan güneşe karşı geleneksel menengiç kahvesi molası.",
            Specialty = "Közde Pişen Menengiç Kahvesi & Badem Şekeri",
            ConciergeTip = "Burçların en üst terasından kaleiçi çatılarının silueti harika fotoğraflanır.",
            DistanceText = "Otele 1.9 km", TransitText = "Yürüyerek 10 dk", EstCost = Math.Round(150m * mult, 0),
            Latitude = hLat + 0.007, Longitude = hLng - 0.015, ImageUrl = "/images/sunset_bar.jpg",
            Address = "Saat Kulesi Meydanı", Rating = 4.8m, BadgeColor = "#ec4899"
        });

        list.Add(new ItineraryStop
        {
            Id = 6, Day = 1, TimeSlot = "20:30 - 23:00",
            Title = "Tarihi Taş Kervansarayda Klasik Türk Musikisi & Akşam Yemeği",
            Category = "Dinner", CategoryName = "Kültürel Akşam Yemeği", CategoryIcon = "🍷",
            Description = "Taş kemerli kubbe altında kanun ve ney ezgileri eşliğinde güveçte kuzu tandır, zeytinyağlı enginar ve fıstıklı baklava.",
            Specialty = "Taş Fırın Kuzu Tandır & Antep Fıstıklı Çıtır Baklava",
            ConciergeTip = "Geleneksel kahve falı ikramı otelimiz misafirlerine özeldir.",
            DistanceText = "Otele 2.1 km", TransitText = "Taksiyle 5 dk", EstCost = Math.Round(950m * mult, 0),
            Latitude = hLat + 0.012, Longitude = hLng - 0.010, ImageUrl = "/images/mardin_mansion.jpg",
            Address = "Tarihi Kervansaray Restoranı", Rating = 4.9m, BadgeColor = "#f59e0b"
        });

        if (days >= 2)
        {
            list.Add(new ItineraryStop
            {
                Id = 7, Day = 2, TimeSlot = "09:30 - 13:00",
                Title = city.Contains("Kapadokya") 
                    ? "Derinkuyu Yeraltı Şehri Tünelleri & Kaya Kiliseleri" 
                    : (city.Contains("Mardin") ? "Dara Antik Kenti & Devasa Yeraltı Zindanı" : "Aspendos & Perge Antik Tiyatroları Keşif Turu"),
                Category = "Culture", CategoryName = "Antik Tiyatro & Akropolis", CategoryIcon = "🏛️",
                Description = "Akustiği günümüze kadar bozulmadan gelen 2000 yıllık antik tiyatro basamaklarında yankılanan tarih ve stadyum kalıntıları.",
                Specialty = "Mükemmel Akustikli Antik Tiyatro Sahnesi",
                ConciergeTip = "Tiyatronun en üst sırasına çıkıp fısıltı testini deneyiniz.",
                DistanceText = "Otele 35 km", TransitText = "VIP Transferle 30 dk", EstCost = Math.Round(450m * mult, 0),
                Latitude = hLat + 0.060, Longitude = hLng + 0.080, ImageUrl = "/images/historical_monument.jpg",
                Address = "Aspendos Antik Kenti", Rating = 5.0m, BadgeColor = "#0284c7"
            });

            list.Add(new ItineraryStop
            {
                Id = 8, Day = 2, TimeSlot = "13:30 - 15:00",
                Title = "Tarihi Köyde Güveçte Yöresel Köfte & Piyaz",
                Category = "Lunch", CategoryName = "Yöresel Tarihi Lezzetler", CategoryIcon = "🍽️",
                Description = "Asırlık çınar ağacı altında güveçte köfte, tahinli piyaz ve manda yoğurdu.",
                Specialty = "Tahinli Coğrafi İşaretli Piyaz & Şiş Köfte",
                ConciergeTip = "Tahinli piyazın sosu özel tahta tokmakla dövülerek hazırlanır.",
                DistanceText = "Antik Kent Yanı", TransitText = "Yürüyerek 5 dk", EstCost = Math.Round(400m * mult, 0),
                Latitude = hLat + 0.058, Longitude = hLng + 0.078, ImageUrl = "/images/aegean_dining.jpg",
                Address = "Tarihi Çınaraltı Köftecisi", Rating = 4.8m, BadgeColor = "#10b981"
            });

            list.Add(new ItineraryStop
            {
                Id = 9, Day = 2, TimeSlot = "19:30 - 22:30",
                Title = "Tarihi Konak Terasında Akşam Yemeği & Şehir Işıkları",
                Category = "Dinner", CategoryName = "Tarihi Teras Yemeği", CategoryIcon = "🍷",
                Description = "Tarihi şehrin taş kulelerini seyrederek yöresel meze tabağı, kuzu pirzola ve fırın sütlaç.",
                Specialty = "Fırında Sakızlı Sütlaç & Marine Kuzu Pirzola",
                ConciergeTip = "Terastan antik limanın gece aydınlatması kartpostallıktır.",
                DistanceText = "Otele 2.0 km", TransitText = "Taksiyle 6 dk", EstCost = Math.Round(850m * mult, 0),
                Latitude = hLat + 0.010, Longitude = hLng - 0.014, ImageUrl = "/images/sunset_bar.jpg",
                Address = "Tarihi Konak Teras Restoranı", Rating = 4.9m, BadgeColor = "#f59e0b"
            });
        }

        if (days >= 3)
        {
            list.Add(new ItineraryStop
            {
                Id = 10, Day = 3, TimeSlot = "10:00 - 13:00",
                Title = "Tarihi Bakırcılar, Baharatçılar & El Dokuma Halı Çarşısı",
                Category = "Activity", CategoryName = "Tarihi Çarşı Gezisi", CategoryIcon = "🛍️",
                Description = "Körük sesleri arasında bakır döven ustalar, kök boyalı kilimler ve antik sikkeler arasında tarihi çarşı keşfi.",
                Specialty = "El Dövmesi Bakır Cezve & Doğal Baharat Karışımları",
                ConciergeTip = "Otantik hediyelik alırken ustalarla sohbet edip çaylarını içiniz.",
                DistanceText = "Otele 1.5 km", TransitText = "Yürüyerek 10 dk", EstCost = Math.Round(400m * mult, 0),
                Latitude = hLat + 0.006, Longitude = hLng - 0.009, ImageUrl = "/images/historical_monument.jpg",
                Address = "Tarihi Bedesten Kapalı Çarşı", Rating = 4.8m, BadgeColor = "#0284c7"
            });

            list.Add(new ItineraryStop
            {
                Id = 11, Day = 3, TimeSlot = "14:00 - 16:30",
                Title = "Tarihi Köşk Bahçesinde Veda Çayı & VIP Uğurlama",
                Category = "Departure", CategoryName = "Tarihi Veda & Transfer", CategoryIcon = "🛫",
                Description = "Köşk bahçesinde taze demlenmiş çay ve özel hazırlanan Kültür Kaşifi Sertifikası eşliğinde konforlu uğurlama.",
                Specialty = "Osmanlı Saray Şerbeti & Fıstıklı Lokum",
                ConciergeTip = "VIP aracınız kalkış saatinde sizi kapıda bekler.",
                DistanceText = "Otel Lobi", TransitText = "0 dk", EstCost = Math.Round(500m * mult, 0),
                Latitude = hLat, Longitude = hLng, ImageUrl = "/images/room_suite.jpg",
                Address = hotel.Address ?? "Otel Lobi", Rating = 5.0m, BadgeColor = "#38bdf8"
            });
        }

        return list;
    }

    // ==========================================
    // 👨‍👩‍👧‍👦 4. ÇOCUKLU AİLE & GÜVENLİ KEŞİF ROTASI
    // ==========================================
    private List<ItineraryStop> GenerateFamilyStops(Hotel hotel, string city, int days, decimal mult, double hLat, double hLng)
    {
        var list = new List<ItineraryStop>();

        list.Add(new ItineraryStop
        {
            Id = 1, Day = 1, TimeSlot = "09:00 - 10:30",
            Title = "Çocuk Büfeli & Pancake Dolu Neşeli Aile Kahvaltısı",
            Category = "Breakfast", CategoryName = "Çocuk Dostu Aile Kahvaltısı", CategoryIcon = "🍳",
            Description = "Renkli meyve tabakları, taze waffle ve pancake istasyonu, taze sıkılmış elma-portakal suları ile neşeli başlangıç.",
            Specialty = "Çikolatalı Waffle Kulesi & Muzlu Pancake",
            ConciergeTip = "Bebek mama sandalyeleri ve çocuk servis setleri görevlilerimizce anında masanıza getirilir.",
            DistanceText = "Otel Bahçesi", TransitText = "0 dk", EstCost = Math.Round(350m * mult, 0),
            Latitude = hLat, Longitude = hLng, ImageUrl = "/images/aegean_dining.jpg",
            Address = hotel.Address ?? "Otel Aile Restoranı", Rating = 4.9m, BadgeColor = "#f59e0b"
        });

        list.Add(new ItineraryStop
        {
            Id = 2, Day = 1, TimeSlot = "11:00 - 14:00",
            Title = city.Contains("Kapadokya") 
                ? "Masal Vadisi Çömlek Atölyesi & Oyuncak Müzesi" 
                : "Dev Tünel Akvaryumu & Tropik Sürüngen Dünyası",
            Category = "Family", CategoryName = "Dev Tünel Akvaryumu", CategoryIcon = "🐬",
            Description = "131 metre uzunluğuyla dünyanın en uzun akvaryum tünelinde köpekbalıkları, dev vatozlar ve rengarenk mercan balıklarıyla tanışma.",
            Specialty = "Köpekbalığı Besleme Saati & Kar Dünyası Buz Kaydırağı",
            ConciergeTip = "Saat 12:00'de akvaryum içi dalgıçların balık besleme gösterisi kaçırılmamalıdır.",
            DistanceText = "Otele 4.5 km", TransitText = "Aile Servisiyle 10 dk", EstCost = Math.Round(850m * mult, 0),
            Latitude = hLat + 0.025, Longitude = hLng - 0.030, ImageUrl = "/images/scuba_diving.jpg",
            Address = "Dev Akvaryum Kompleksi", Rating = 5.0m, BadgeColor = "#0284c7"
        });

        list.Add(new ItineraryStop
        {
            Id = 3, Day = 1, TimeSlot = "14:15 - 15:30",
            Title = "Taş Fırın İtalyan Pizza & Çıtır Patates Öğle Yemeği",
            Category = "Lunch", CategoryName = "Çocuk Dostu Öğle Yemeği", CategoryIcon = "🍕",
            Description = "İnce çıtır hamurlu taş fırın margherita pizza, ev yapımı köfte burger ve taze limonata.",
            Specialty = "Taş Fırın 4 Peynirli Pizza & Ev Yapımı Çilekli Limonata",
            ConciergeTip = "Restoran bahçesinde çocuk oyun parkı ve gözetmen abla bulunmaktadır.",
            DistanceText = "Akvaryum Çıkışı", TransitText = "Yürüyerek 2 dk", EstCost = Math.Round(450m * mult, 0),
            Latitude = hLat + 0.026, Longitude = hLng - 0.029, ImageUrl = "/images/aegean_dining.jpg",
            Address = "Aile Bahçe Restoranı", Rating = 4.8m, BadgeColor = "#10b981"
        });

        list.Add(new ItineraryStop
        {
            Id = 4, Day = 1, TimeSlot = "16:00 - 18:30",
            Title = "Sığ & Mavi Bayraklı Kum Plajda Güvenli Deniz & Kumdan Kale",
            Category = "Beach", CategoryName = "Güvenli Çocuk Kum Plajı", CategoryIcon = "🏖️",
            Description = "50 metre boyunca derinleşmeyen, dalgasız ve tertemiz kum plajda çocuklarla kumdan kale yapma ve güvenli yüzme.",
            Specialty = "Mavi Bayraklı Sığ Deniz & Kum Kalesi Yarışması",
            ConciergeTip = "Otel cankurtaranları ve çocuk can yelekleri plaj kabininde ücretsizdir.",
            DistanceText = "Otele 1.5 km", TransitText = "Plaj Servisiyle 4 dk", EstCost = Math.Round(200m * mult, 0),
            Latitude = hLat - 0.010, Longitude = hLng + 0.015, ImageUrl = "/images/sunset_bar.jpg",
            Address = "Mavi Bayrak Aile Halk Plajı", Rating = 4.9m, BadgeColor = "#38bdf8"
        });

        list.Add(new ItineraryStop
        {
            Id = 5, Day = 1, TimeSlot = "19:00 - 20:30",
            Title = "Işıklı Dans Eden Fıskiyeler & Açık Hava Dondurma Şöleni",
            Category = "Fun", CategoryName = "Dondurma & Müzik Parkı", CategoryIcon = "🍦",
            Description = "Müzik eşliğinde dans eden renkli su fıskiyeleri kenarında Maraş dondurması ve çikolatalı krep keyfi.",
            Specialty = "Hakiki Keçi Sütü Maraş Dondurması & Çikolata Şelalesi",
            ConciergeTip = "Dondurmacının eğlenceli şovları çocukların neşesini ikiye katlar.",
            DistanceText = "Otele 1.2 km", TransitText = "Yürüyerek 8 dk", EstCost = Math.Round(180m * mult, 0),
            Latitude = hLat + 0.005, Longitude = hLng - 0.005, ImageUrl = "/images/sunset_bar.jpg",
            Address = "Kent Park Fıskiye Meydanı", Rating = 4.9m, BadgeColor = "#ec4899"
        });

        list.Add(new ItineraryStop
        {
            Id = 6, Day = 1, TimeSlot = "20:45 - 22:30",
            Title = "Çocuk Dostu Bahçe Restoranında Akşam Yemeği & Mini Tiyatro",
            Category = "Dinner", CategoryName = "Aile Akşam Yemeği & Gösteri", CategoryIcon = "🍽️",
            Description = "Geniş çim bahçede çocuk tiyatrosu ve illüzyon gösterisi eşliğinde ızgara köfteler, taze sebzeler ve meyveli tatlılar.",
            Specialty = "Çocuk Menüsü & Izgara Köfte / Çıtır Tavuk",
            ConciergeTip = "Yemek sırasında palyaço ve yüz boyama etkinlikleri tamamen ücretsizdir.",
            DistanceText = "Otele 2.0 km", TransitText = "Taksiyle 5 dk", EstCost = Math.Round(750m * mult, 0),
            Latitude = hLat + 0.012, Longitude = hLng - 0.010, ImageUrl = "/images/aegean_dining.jpg",
            Address = "Çim Bahçe Aile Kulübü", Rating = 4.9m, BadgeColor = "#f59e0b"
        });

        if (days >= 2)
        {
            list.Add(new ItineraryStop
            {
                Id = 7, Day = 2, TimeSlot = "10:00 - 15:00",
                Title = "The Land of Legends Efsaneler Parkı & Su Kaydırakları",
                Category = "ThemePark", CategoryName = "Tema Park & Su Dünyası", CategoryIcon = "🎢",
                Description = "Dev hız trenleri, dalga havuzları, su kaydırakları ve masal şatosuyla çocukların hayal dünyasını gerçeğe dönüştüren devasa tema parkı.",
                Specialty = "Dalga Havuzu & Efsaneler Kalesi Su Gösterisi",
                ConciergeTip = "Bileklik ile sıra beklemeden hızlı geçiş (Fast Track) otelimizce sağlanır.",
                DistanceText = "Otele 25 km", TransitText = "Aile Servisiyle 25 dk", EstCost = Math.Round(1800m * mult, 0),
                Latitude = hLat - 0.030, Longitude = hLng + 0.060, ImageUrl = "/images/room_family.jpg",
                Address = "Tema Park Efsaneler Bölgesi", Rating = 5.0m, BadgeColor = "#8b5cf6"
            });

            list.Add(new ItineraryStop
            {
                Id = 8, Day = 2, TimeSlot = "19:30 - 22:00",
                Title = "Yıldızlar Altında Açık Hava Aile Sineması & Barbekü",
                Category = "Night", CategoryName = "Açık Hava Aile Sineması", CategoryIcon = "🍿",
                Description = "Yumuşak armut koltuklarda patlamış mısır eşliğinde animasyon film gösterimi ve hafif sahil barbeküsü.",
                Specialty = "Taze Patlamış Mısır & Izgara Mini Burgerler",
                ConciergeTip = "Çocuklar için peluş battaniyeler görevlilerimizce dağıtılır.",
                DistanceText = "Otel Bahçesi", TransitText = "0 dk", EstCost = Math.Round(500m * mult, 0),
                Latitude = hLat, Longitude = hLng, ImageUrl = "/images/sunset_bar.jpg",
                Address = hotel.Address ?? "Otel Açık Hava Sinema Alanı", Rating = 4.9m, BadgeColor = "#f59e0b"
            });
        }

        if (days >= 3)
        {
            list.Add(new ItineraryStop
            {
                Id = 9, Day = 3, TimeSlot = "10:30 - 13:30",
                Title = "MasterChef Kids: Çikolata & Pizza Yapım Atölyesi",
                Category = "Activity", CategoryName = "Çocuk Mutfak Atölyesi", CategoryIcon = "👨‍🍳",
                Description = "Çocukların şef önlüğü ve aşçı şapkası takarak kendi çikolatalarını ve pizzalarını şekillendirdiği eğlenceli atölye.",
                Specialty = "Kendi Yaptıkları Çikolata Kutusu & Küçük Şef Sertifikası",
                ConciergeTip = "Atölye sonunda her çocuğa ismine özel aşçı kepi hediye edilir.",
                DistanceText = "Otele 2.2 km", TransitText = "Aile Servisi", EstCost = Math.Round(550m * mult, 0),
                Latitude = hLat + 0.010, Longitude = hLng - 0.012, ImageUrl = "/images/aegean_dining.jpg",
                Address = "Gurme Çocuk Mutfak Sanatları", Rating = 5.0m, BadgeColor = "#ec4899"
            });

            list.Add(new ItineraryStop
            {
                Id = 10, Day = 3, TimeSlot = "14:00 - 16:30",
                Title = "Aile Veda Yemeği & Özel Fotoğraf Çekimi",
                Category = "Departure", CategoryName = "Aile Veda & VIP Transfer", CategoryIcon = "🛫",
                Description = "Tüm ailenin bir arada olduğu neşeli veda yemeği, aile fotoğraf albümü hediyesi ve çocuk koltuklu VIP transfer.",
                Specialty = "Özel Aile Fotoğraf Çerçevesi & Fırın Sütlaç",
                ConciergeTip = "VIP araçta bebek ve çocuk güvenlik koltuğu standarttır.",
                DistanceText = "Otel Lobi", TransitText = "0 dk", EstCost = Math.Round(600m * mult, 0),
                Latitude = hLat, Longitude = hLng, ImageUrl = "/images/room_suite.jpg",
                Address = hotel.Address ?? "Otel Lobi", Rating = 5.0m, BadgeColor = "#38bdf8"
            });
        }

        return list;
    }

    // ==========================================
    // 🍽️ 5. GURME GASTRONOMİ & ŞARAP TADIMI
    // ==========================================
    private List<ItineraryStop> GenerateGourmetStops(Hotel hotel, string city, int days, decimal mult, double hLat, double hLng)
    {
        var list = new List<ItineraryStop>();

        list.Add(new ItineraryStop
        {
            Id = 1, Day = 1, TimeSlot = "09:00 - 10:30",
            Title = "Yerel Köylü Pazarında Çıtır Börek & Sıcak Manda Kaymağı",
            Category = "Breakfast", CategoryName = "Gurme Köy Pazarı Kahvaltısı", CategoryIcon = "🍳",
            Description = "Taş fırından yeni çıkmış el açması çıtır börekler, taze petek balı, manda kaymağı ve taze keçi tulumu.",
            Specialty = "El Açması Çıtır Ispanaklı Börek & Manda Kaymağı",
            ConciergeTip = "Köylü pazarındaki zeytin üreticilerinden kırma yeşil zeytin tadımı yapabilirsiniz.",
            DistanceText = "Otele 2.5 km", TransitText = "Taksiyle 6 dk", EstCost = Math.Round(350m * mult, 0),
            Latitude = hLat + 0.012, Longitude = hLng - 0.015, ImageUrl = "/images/aegean_dining.jpg",
            Address = "Tarihi Köy Pazarı Avlusu", Rating = 4.9m, BadgeColor = "#f59e0b"
        });

        list.Add(new ItineraryStop
        {
            Id = 2, Day = 1, TimeSlot = "11:00 - 13:30",
            Title = "Soğuk Sıkım Natürel Sızma Zeytinyağı Tadım Laboratuvarı",
            Category = "Activity", CategoryName = "Zeytinyağı Tadım Atölyesi", CategoryIcon = "🫒",
            Description = "Asırlık zeytinlikler arasında sertifikalı tadım uzmanlarıyla erken hasat polifenol zengini yağları ayırt etme eğitimi.",
            Specialty = "Erken Hasat Soğuk Sıkım Memecik & Ayvalık Yağları",
            ConciergeTip = "Tadımda mavi cam kadehler kullanılarak rengin değil koku ve tadın ön plana çıkması sağlanır.",
            DistanceText = "Otele 8.0 km", TransitText = "Transferle 12 dk", EstCost = Math.Round(450m * mult, 0),
            Latitude = hLat + 0.030, Longitude = hLng + 0.020, ImageUrl = "/images/aegean_dining.jpg",
            Address = "Zeytinlik ve Tadım Enstitüsü", Rating = 5.0m, BadgeColor = "#10b981"
        });

        list.Add(new ItineraryStop
        {
            Id = 3, Day = 1, TimeSlot = "13:45 - 15:30",
            Title = "Taş Fırın Tandır & Şefin Yöresel Tadım Tabakları",
            Category = "Lunch", CategoryName = "Taş Fırın Tandır Ziyafeti", CategoryIcon = "🍽️",
            Description = "Kuyu tandırda 6 saat kısık ateşte pişen kuzu but, iç pilav, közlenmiş patlıcan salatası ve vişneli ekmek kadayıfı.",
            Specialty = "Kuyu Kuzu Tandır & Vişneli Kaymaklı Ekmek Kadayıfı",
            ConciergeTip = "Şef masasında oturan misafirlerimize tandırın açılış seremonisi izletilir.",
            DistanceText = "Otele 4.2 km", TransitText = "Taksiyle 8 dk", EstCost = Math.Round(950m * mult, 0),
            Latitude = hLat + 0.018, Longitude = hLng - 0.022, ImageUrl = "/images/mardin_mansion.jpg",
            Address = "Tarihi Taş Ocak Restoranı", Rating = 4.9m, BadgeColor = "#f59e0b"
        });

        list.Add(new ItineraryStop
        {
            Id = 4, Day = 1, TimeSlot = "16:00 - 18:30",
            Title = "Bağ Bozumu & Sommelier ile 5 Kadeh Butik Şarap Eşleşmesi",
            Category = "Tasting", CategoryName = "Butik Bağ & Mahzen Tadımı", CategoryIcon = "🍇",
            Description = "Fransız meşe fıçılarda yıllanmış Şiraz, Kalecik Karası ve Öküzgözü rezerve şaraplarının peynirlerle buluşması.",
            Specialty = "Öküzgözü Rezerve & Yıllanmış İsli Çerkes Peyniri",
            ConciergeTip = "Sommelier eşliğinde şarabın koku çemberi kartı her misafire hediye edilir.",
            DistanceText = "Otele 14 km", TransitText = "VIP Transferle 16 dk", EstCost = Math.Round(1400m * mult, 0),
            Latitude = hLat - 0.040, Longitude = hLng + 0.035, ImageUrl = "/images/aegean_dining.jpg",
            Address = "Bağ Tesisi & Şarap Mahzeni", Rating = 5.0m, BadgeColor = "#8b5cf6"
        });

        list.Add(new ItineraryStop
        {
            Id = 5, Day = 1, TimeSlot = "19:00 - 20:30",
            Title = "Panoramik Çatıda Gün Batımı Eşliğinde Deniz Ürünlü Meze Tepsisi",
            Category = "Sunset", CategoryName = "Gün Batımı Meze Terası", CategoryIcon = "🌅",
            Description = "Ahtapot salatası, deniz börülcesi, fava, ızgara kalamar ve aslan sütü ile ufukta batış manzarası.",
            Specialty = "Zeytinyağlı Deniz Börülcesi & Izgara Ahtapot Bacağı",
            ConciergeTip = "Güneşin tam ufuk çizgisinde kaybolduğu anda kadehler tokuşturulur.",
            DistanceText = "Otele 2.0 km", TransitText = "Taksiyle 5 dk", EstCost = Math.Round(650m * mult, 0),
            Latitude = hLat + 0.008, Longitude = hLng - 0.012, ImageUrl = "/images/sunset_bar.jpg",
            Address = "Çatı Meze Terası", Rating = 4.9m, BadgeColor = "#ec4899"
        });

        list.Add(new ItineraryStop
        {
            Id = 6, Day = 1, TimeSlot = "20:45 - 23:30",
            Title = "Michelin Seçkisi Şef Restoranında 7 Aşamalı Degustasyon Menüsü",
            Category = "Dinner", CategoryName = "7 Aşamalı Michelin Degustasyon", CategoryIcon = "🍷",
            Description = "Türkiye'nin en seçkin şeflerinden birinin mutfağında hazırlanan trüflü levrek, kabak çiçeği dolması, dinlendirilmiş antrikot ve altın yapraklı tatlı.",
            Specialty = "Trüflü Levrek Carpaccio & Şefin İmza Çikolata Küresi",
            ConciergeTip = "Şef her tabağı bizzat masanıza gelerek hikayesiyle sunar.",
            DistanceText = "Otele 3.8 km", TransitText = "VIP Transferle 8 dk", EstCost = Math.Round(2600m * mult, 0),
            Latitude = hLat + 0.020, Longitude = hLng - 0.030, ImageUrl = "/images/aegean_dining.jpg",
            Address = "Şef Restoranı & Gastro Bar", Rating = 5.0m, BadgeColor = "#f59e0b"
        });

        if (days >= 2)
        {
            list.Add(new ItineraryStop
            {
                Id = 7, Day = 2, TimeSlot = "10:00 - 13:00",
                Title = "Şef ile Birlikte Balık Hali Gezisi & Kendi Balığını Pişirme Atölyesi",
                Category = "Activity", CategoryName = "Gastro Balıkçı Atölyesi", CategoryIcon = "🐟",
                Description = "Sabah mezatında taze deniz balıklarının seçimi, temizlenmesi ve şefin tüyolarıyla tuzda balık pişirme tekniği.",
                Specialty = "Tuzda Levrek Alev Gösterisi & Karides Güveç",
                ConciergeTip = "Tuz kabuğunu kırma çekicini siz kullanabilirsiniz; harika bir video anısıdır.",
                DistanceText = "Otele 3.0 km", TransitText = "Transferle 6 dk", EstCost = Math.Round(1200m * mult, 0),
                Latitude = hLat + 0.015, Longitude = hLng - 0.020, ImageUrl = "/images/boat_tour.jpg",
                Address = "Balık Hali & Pişirme Akademisi", Rating = 4.9m, BadgeColor = "#0284c7"
            });

            list.Add(new ItineraryStop
            {
                Id = 8, Day = 2, TimeSlot = "19:30 - 23:00",
                Title = "Gizli Mahzende Kuru Dinlendirilmiş (Dry-Aged) Et & Şarap Ziyafeti",
                Category = "Dinner", CategoryName = "Mahzen Dry-Aged Steakhouse", CategoryIcon = "🥩",
                Description = "Himalaya tuzu odalarında 45 gün dinlendirilmiş T-bone ve Tomahawk etlerin kömür ızgarasında pişirilişi.",
                Specialty = "45 Gün Dinlendirilmiş Tomahawk Steak & Trüflü Patates Püresi",
                ConciergeTip = "Masada alevli tereyağı dökme şovu yapılır.",
                DistanceText = "Otele 2.8 km", TransitText = "Taksiyle 7 dk", EstCost = Math.Round(2200m * mult, 0),
                Latitude = hLat + 0.010, Longitude = hLng - 0.018, ImageUrl = "/images/sunset_bar.jpg",
                Address = "Tarihi Mahzen Steakhouse", Rating = 5.0m, BadgeColor = "#f59e0b"
            });
        }

        if (days >= 3)
        {
            list.Add(new ItineraryStop
            {
                Id = 9, Day = 3, TimeSlot = "10:30 - 13:00",
                Title = "Geleneksel Taş Ocakta Çıtır Katmer & Künefe Atölyesi",
                Category = "Activity", CategoryName = "Geleneksel Tatlı Zanaatı", CategoryIcon = "🥞",
                Description = "İncecik açılan yufka içerisine bol Antep fıstığı ve hakiki kaymakla hazırlanan sıcak katmer.",
                Specialty = "Taş Fırında Çıtır Fıstıklı Katmer & Maraş Dondurması",
                ConciergeTip = "Sıcak katmerin yanında soğuk manda sütü içilmesi önerilir.",
                DistanceText = "Otele 1.8 km", TransitText = "Yürüyerek 12 dk", EstCost = Math.Round(450m * mult, 0),
                Latitude = hLat + 0.007, Longitude = hLng - 0.010, ImageUrl = "/images/aegean_dining.jpg",
                Address = "Tarihi Katmerci & Tatlı Evi", Rating = 5.0m, BadgeColor = "#ec4899"
            });

            list.Add(new ItineraryStop
            {
                Id = 10, Day = 3, TimeSlot = "14:00 - 16:30",
                Title = "Gurme Baharat & Yerel Lezzet Sepeti Hediyesi & VIP Transfer",
                Category = "Departure", CategoryName = "Gurme Veda & Transfer", CategoryIcon = "🛫",
                Description = "Özel vakumlu gurme peynirler, zeytinyağı ve baharatlardan oluşan hediye paketi ve VIP havalimanı transferi.",
                Specialty = "Özel Seçkili Gurme Lezzet Sandığı",
                ConciergeTip = "Tüm paketler uçak kabinine uygun korumalı ambalajlanmıştır.",
                DistanceText = "Otel Lobi", TransitText = "0 dk", EstCost = Math.Round(750m * mult, 0),
                Latitude = hLat, Longitude = hLng, ImageUrl = "/images/room_suite.jpg",
                Address = hotel.Address ?? "Otel Lobi", Rating = 5.0m, BadgeColor = "#38bdf8"
            });
        }

        return list;
    }

    // ==========================================
    // 🧘 6. WELLNESS, SPA & HUZUR MOLASI
    // ==========================================
    private List<ItineraryStop> GenerateWellnessStops(Hotel hotel, string city, int days, decimal mult, double hLat, double hLng)
    {
        var list = new List<ItineraryStop>();

        list.Add(new ItineraryStop
        {
            Id = 1, Day = 1, TimeSlot = "08:00 - 09:30",
            Title = "Deniz / Doğa Kıyısında Gün Doğumu Yogası & Taze Yeşil Detoks Suyu",
            Category = "Yoga", CategoryName = "Gün Doğumu Yogası & Meditasyon", CategoryIcon = "🧘",
            Description = "Dalga ve kuş sesleri eşliğinde uzman eğitmen ile nefes egzersizleri, esneme yogası ve taze kereviz-yeşil elma detoks ikramı.",
            Specialty = "Pranayama Nefes Egzersizi & Taze Zencefilli Yeşil Detoks Suyu",
            ConciergeTip = "Yoga matları ve organik pamuk havlular eğitmenlerimizce hazır bekletilir.",
            DistanceText = "Otel Sahil İskelesi", TransitText = "0 dk", EstCost = Math.Round(350m * mult, 0),
            Latitude = hLat, Longitude = hLng, ImageUrl = "/images/sunset_bar.jpg",
            Address = hotel.Address ?? "Otel Yoga Platformu", Rating = 5.0m, BadgeColor = "#10b981"
        });

        list.Add(new ItineraryStop
        {
            Id = 2, Day = 1, TimeSlot = "09:45 - 11:00",
            Title = "Glutensiz & Organik Avokadolu Wellness Kahvaltısı",
            Category = "Breakfast", CategoryName = "Organik Wellness Kahvaltısı", CategoryIcon = "🥑",
            Description = "Karahindiba çayı, avokadolu ekşi mayalı poşe yumurta, taze böğürtlenler ve chia tohumlu badem sütü kasesi.",
            Specialty = "Ekşi Maya Avokado Poşe & Chia Puding",
            ConciergeTip = "Kahvaltı sonrasında metabolizma hızlandırıcı matcha çayı servis edilir.",
            DistanceText = "Otel Bahçe Restoranı", TransitText = "0 dk", EstCost = Math.Round(380m * mult, 0),
            Latitude = hLat, Longitude = hLng, ImageUrl = "/images/aegean_dining.jpg",
            Address = hotel.Address ?? "Otel Sağlıklı Yaşam Terası", Rating = 4.9m, BadgeColor = "#f59e0b"
        });

        list.Add(new ItineraryStop
        {
            Id = 3, Day = 1, TimeSlot = "11:30 - 14:30",
            Title = "Tarihi Mermer Hamamda Kese, Köpük & Aromaterapi Masajı",
            Category = "Spa", CategoryName = "Geleneksel Türk Hamamı Ritüeli", CategoryIcon = "🛁",
            Description = "Göbek taşında terleme sonrası ipek kese, zeytinyağlı sabun köpüğü banyosu ve lavanta yağları ile 60 dakikalık rahatlatıcı masaj.",
            Specialty = "İpek Kese, Hakiki Defne Köpüğü & Lavanta Yağı Masajı",
            ConciergeTip = "Hamam çıkışında soğuk ayran ve taze adaçayı ikram edilir.",
            DistanceText = "Otel İçi Spa", TransitText = "0 dk", EstCost = Math.Round(1450m * mult, 0),
            Latitude = hLat, Longitude = hLng, ImageUrl = "/images/room_suite.jpg",
            Address = "Otel Tarihi Spa Katı", Rating = 5.0m, BadgeColor = "#8b5cf6"
        });

        list.Add(new ItineraryStop
        {
            Id = 4, Day = 1, TimeSlot = "15:00 - 16:30",
            Title = "Sessiz Bahçede Bitki Çayı & Hafif Akdeniz Salataları",
            Category = "Lunch", CategoryName = "Detoks & Hafif Öğle Yemeği", CategoryIcon = "🥗",
            Description = "Kinoa salatası, ızgara enginar kalbi, taze nane ve hafif zeytinyağlı Ege mezeleri.",
            Specialty = "Kinoalı Izgara Enginar Salatası & Taze Ihlamur Çayı",
            ConciergeTip = "Sessizlik bahçesinde telefonların sessize alınması rica edilir.",
            DistanceText = "Otel Bahçesi", TransitText = "0 dk", EstCost = Math.Round(450m * mult, 0),
            Latitude = hLat, Longitude = hLng, ImageUrl = "/images/aegean_dining.jpg",
            Address = "Sessiz Zen Bahçesi", Rating = 4.8m, BadgeColor = "#10b981"
        });

        list.Add(new ItineraryStop
        {
            Id = 5, Day = 1, TimeSlot = "17:00 - 18:30",
            Title = "Tuz Odası Terapi Seansı & Termal Havuz Dinlenmesi",
            Category = "Wellness", CategoryName = "Himalaya Tuz Odası Terapisi", CategoryIcon = "🧂",
            Description = "Mikro tuz parçacıklarıyla nefes yollarını arındıran doğal tuz mağarası ortamı ve 36 derece ılık termal dinlenme havuzu.",
            Specialty = "Himalaya Tuzu İnhalasyon Seansı & Mineral Havuzu",
            ConciergeTip = "Seans boyunca gözlerinizi kapatıp derin diyafram nefesi alınız.",
            DistanceText = "Otel Spa Bölümü", TransitText = "0 dk", EstCost = Math.Round(600m * mult, 0),
            Latitude = hLat, Longitude = hLng, ImageUrl = "/images/room_suite.jpg",
            Address = "Otel Termal Spa Alanı", Rating = 5.0m, BadgeColor = "#06b6d4"
        });

        list.Add(new ItineraryStop
        {
            Id = 6, Day = 1, TimeSlot = "19:30 - 22:00",
            Title = "Akustik Müzik Eşliğinde Hafif Şef Akşam Yemeği & Buharda Somon",
            Category = "Dinner", CategoryName = "Sağlıklı Gurme Akşam Yemeği", CategoryIcon = "🍽️",
            Description = "Hafif piyano melodileri eşliğinde buharda kuşkonmazlı Norveç somonu, fırınlanmış kök sebzeler ve şekersiz fırın elma tatlısı.",
            Specialty = "Buharda Somon Fileto & Şekersiz Fırın Ayva Tatlısı",
            ConciergeTip = "Yemek sonrasında melisa ve papatya çayı uykuyu derinleştirir.",
            DistanceText = "Otele 1.5 km", TransitText = "Taksiyle 4 dk", EstCost = Math.Round(1100m * mult, 0),
            Latitude = hLat + 0.008, Longitude = hLng - 0.010, ImageUrl = "/images/sunset_bar.jpg",
            Address = "Sakin Sahil Restoranı", Rating = 4.9m, BadgeColor = "#f59e0b"
        });

        if (days >= 2)
        {
            list.Add(new ItineraryStop
            {
                Id = 7, Day = 2, TimeSlot = "09:00 - 12:00",
                Title = "Çam Ormanında Doğa Terapisi (Shinrin-Yoku) & Şifalı Bitki Toplama",
                Category = "Activity", CategoryName = "Orman Banyosu & Doğa Yürüyüşü", CategoryIcon = "🌲",
                Description = "Japon orman banyosu felsefesiyle çam iğneleri kokusunda sessiz doğa yürüyüşü ve yabani kekik toplama.",
                Specialty = "Shinrin-Yoku Ses Terapisi & Yaban Kekiği Çayı",
                ConciergeTip = "Topladığınız taze adaçayları otelde adınıza kurutulup paketlenir.",
                DistanceText = "Otele 8.0 km", TransitText = "Transferle 12 dk", EstCost = Math.Round(350m * mult, 0),
                Latitude = hLat + 0.035, Longitude = hLng + 0.025, ImageUrl = "/images/duden_waterfall.jpg",
                Address = "Milli Park Orman Parkuru", Rating = 5.0m, BadgeColor = "#10b981"
            });

            list.Add(new ItineraryStop
            {
                Id = 8, Day = 2, TimeSlot = "14:00 - 17:00",
                Title = "Sıcak Taş Masajı & Hidroterapi Jakuzisi",
                Category = "Spa", CategoryName = "Sıcak Volkanik Taş Terapisi", CategoryIcon = "💆",
                Description = "Volkanik bazalt taşlarının vücudun enerji merkezlerine yerleştirilmesiyle kaslardaki tüm gerginliğin çözülmesi.",
                Specialty = "Volkanik Taş Masajı & Nane Özlü Jakuzi Banyosu",
                ConciergeTip = "Tüm seans süresince sakinleştirici su damlası melodileri çalar.",
                DistanceText = "Otel İçi Spa", TransitText = "0 dk", EstCost = Math.Round(1350m * mult, 0),
                Latitude = hLat, Longitude = hLng, ImageUrl = "/images/room_suite.jpg",
                Address = "Otel Spa Dinlenme Süiti", Rating = 5.0m, BadgeColor = "#8b5cf6"
            });
        }

        if (days >= 3)
        {
            list.Add(new ItineraryStop
            {
                Id = 9, Day = 3, TimeSlot = "10:00 - 13:00",
                Title = "Termal Çamur Maskesi & Kolajen Cilt Yenileme Terapisi",
                Category = "Beauty", CategoryName = "Doğal Termal Çamur Bakımı", CategoryIcon = "🧖",
                Description = "Mineral bakımından zengin doğal termal çamurun cilde uygulanması ve organik hyaluronik kolajen serum bakımı.",
                Specialty = "Doğal Termal Kükürtlü Çamur Maskesi",
                ConciergeTip = "Uygulama sonrası cildiniz gün boyu ipeksi bir parlaklığa kavuşur.",
                DistanceText = "Otel Spa", TransitText = "0 dk", EstCost = Math.Round(1100m * mult, 0),
                Latitude = hLat, Longitude = hLng, ImageUrl = "/images/room_suite.jpg",
                Address = "Otel Güzellik & Bakım Katı", Rating = 4.9m, BadgeColor = "#06b6d4"
            });

            list.Add(new ItineraryStop
            {
                Id = 10, Day = 3, TimeSlot = "14:00 - 16:30",
                Title = "Detoks Tarifleri Kitapçığı & Sessiz VIP Uğurlama",
                Category = "Departure", CategoryName = "Wellness Veda & Transfer", CategoryIcon = "🛫",
                Description = "Şefin hazırladığı evde detoks rehberi kitapçığı ve sessiz elektrikli VIP transfer ile havalimanına uğurlama.",
                Specialty = "OtelPro Wellness Detoks Tarifleri Kitabı",
                ConciergeTip = "Araçta dinlendirici aromaterapi kokusu ve hafif klasik müzik çalar.",
                DistanceText = "Otel Lobi", TransitText = "0 dk", EstCost = Math.Round(500m * mult, 0),
                Latitude = hLat, Longitude = hLng, ImageUrl = "/images/room_suite.jpg",
                Address = hotel.Address ?? "Otel Lobi", Rating = 5.0m, BadgeColor = "#38bdf8"
            });
        }

        return list;
    }
}
