using Microsoft.EntityFrameworkCore;
using Otel.Web.Data;

namespace Otel.Web.Services;

public record ArrivingGuestFlightInfo(
    string GuestName,
    string RoomNumber,
    string RoomType,
    string ReservationCode,
    string VipStatus,
    string TransferStatus,
    string DriverInfo,
    string EstimatedHotelArrival,
    string Phone = "+90 532 000 00 00",
    string FlightNumber = "",
    string OriginCity = "",
    string OriginCountry = "",
    string OriginFlag = "✈️",
    string FlightStatus = "İndi",
    string ArrivalTime = "14:15",
    string PickupSignText = "",
    string TerminalGate = "VIP Çıkış Kapısı 2",
    string VehiclePlate = "34 VIP 007",
    string VehicleModel = "Mercedes-Maybach Vito VIP",
    string BaggageStatus = "Bavul Bandı 3'te Alındı",
    string RoomReadiness = "✅ Oda Hazırlandı & Havalandırıldı",
    string SpecialWishes = "🍾 Karşılama şampanyası & meyve sepeti hazır"
);

public record FlightInboundInfo(
    string FlightNumber,
    string OriginCity,
    string OriginCountry,
    string OriginFlag,
    string ArrivalTime,
    string AircraftType,
    int Capacity,
    int LoadFactorPercent,
    string Status,
    List<ArrivingGuestFlightInfo> ArrivingGuests,
    string AirlineName = "Türk Hava Yolları",
    string AirlineCode = "TK",
    string TailNumber = "TC-JSE",
    int AltitudeFt = 2400,
    int SpeedKnots = 185,
    int HeadingDeg = 345,
    double DistanceNm = 8.4,
    string Runway = "Pist 36R",
    string Gate = "Körük 104",
    string BaggageBelt = "Bant 3",
    string StatusDetail = "Final Yaklaşmada · ILS Kilitlendi",
    double RadarX = 52.0,
    double RadarY = 42.0
);

public record AirportInflowSummary(
    string AirportCode,
    string AirportName,
    int TotalFlightsToday,
    int TotalInboundPassengers,
    double AverageLoadFactor,
    int PrivateJetLandings,
    double GrowthVsLastWeek,
    string ActiveRunway = "Pist 36R (ILS CAT II)",
    string WindCondition = "330° / 11 kt (Kuzeybatı)",
    string Visibility = "10+ km (CAVOK)",
    string Metar = "METAR LTFE 160820Z 33011KT CAVOK 26/14 Q1014 NOSIG"
);

public record CityRadarEvent(
    int Id,
    int HotelId,
    string Title,
    string Category,
    string CategoryIcon,
    string CategoryColor,
    string Venue,
    double DistanceKm,
    DateTime Date,
    int ExpectedAttendance,
    int TicketSoldPercent,
    int DemandImpactScore,
    string Description,
    bool IsCustom = false
);

public record WeatherTourismImpact(
    string Condition,
    string ConditionIcon,
    int TempC,
    int SeaTempC,
    int UvIndex,
    int TourismDemandScore,
    string ImpactSummary
);

public record YieldSurgeAnalysis(
    decimal BaseAdr,
    double SurgeMultiplier,
    decimal SurgedAdr,
    int DemandIndex,
    string DemandLevel,
    string LevelColor,
    int OccupancyProjected,
    decimal RevParBase,
    decimal RevParSurged,
    decimal ProjectedRevenueLift,
    List<string> KeyDemandDrivers
);

public record OtaSyncChannel(
    string Id,
    string Name,
    string Icon,
    string Color,
    decimal CurrentRate,
    decimal NewRate,
    int LatencyMs,
    bool IsSynced,
    string StatusText
);

public class YieldRadarService(IDbContextFactory<AppDb> dbFactory, NearbyService nearbySvc)
{
    private readonly List<CityRadarEvent> _customSimulatedEvents = new();

    public async Task<List<Hotel>> GetHotelsAsync()
    {
        return await nearbySvc.GetHotelsAsync();
    }

    public AirportInflowSummary GetAirportInflow(Hotel hotel)
    {
        var city = hotel.City.ToLowerInvariant();
        if (city.Contains("bodrum") || city.Contains("muğla"))
        {
            return new AirportInflowSummary(
                "BJV",
                "Milas-Bodrum Uluslararası Havalimanı (BJV)",
                142,
                26850,
                94.6,
                24,
                18.4,
                ActiveRunway: "Pist 36R (ILS CAT II)",
                WindCondition: "330° / 11 kt (Kuzeybatı)",
                Visibility: "10+ km (CAVOK)",
                Metar: "METAR LTFE 160820Z 33011KT CAVOK 26/14 Q1014 NOSIG"
            );
        }
        if (city.Contains("istanbul") || city.Contains("beşiktaş"))
        {
            return new AirportInflowSummary(
                "IST",
                "İstanbul Uluslararası Havalimanı & Sabiha Gökçen (IST/SAW)",
                1280,
                194200,
                92.8,
                48,
                12.7,
                ActiveRunway: "Pist 35L & 35R (Simultaneous ILS CAT III)",
                WindCondition: "020° / 16 kt (Poyraz)",
                Visibility: "10+ km (CAVOK)",
                Metar: "METAR LTFM 160820Z 02016KT 9999 FEW030 21/11 Q1018 NOSIG"
            );
        }
        if (city.Contains("antalya"))
        {
            return new AirportInflowSummary(
                "AYT",
                "Antalya Uluslararası Havalimanı (AYT)",
                620,
                98400,
                96.2,
                19,
                21.5,
                ActiveRunway: "Pist 36C & 18L (ILS CAT II)",
                WindCondition: "350° / 08 kt (Kuzey)",
                Visibility: "10+ km (CAVOK)",
                Metar: "METAR LTAI 160820Z 35008KT CAVOK 28/17 Q1012 NOSIG"
            );
        }
        if (city.Contains("kapadokya") || city.Contains("nevşehir"))
        {
            return new AirportInflowSummary(
                "NAV",
                "Nevşehir Kapadokya Havalimanı (NAV/ASR)",
                48,
                8200,
                91.4,
                8,
                15.2,
                ActiveRunway: "Pist 11 (VOR/DME)",
                WindCondition: "090° / 07 kt (Doğu)",
                Visibility: "10+ km (CAVOK)",
                Metar: "METAR LTAZ 160820Z 09007KT CAVOK 19/08 Q1020 NOSIG"
            );
        }
        if (city.Contains("çeşme") || city.Contains("izmir"))
        {
            return new AirportInflowSummary(
                "ADB",
                "İzmir Adnan Menderes Havalimanı (ADB)",
                210,
                34600,
                93.1,
                14,
                11.8,
                ActiveRunway: "Pist 34R (ILS CAT II)",
                WindCondition: "340° / 12 kt (Kuzey)",
                Visibility: "10+ km (CAVOK)",
                Metar: "METAR LTBJ 160820Z 34012KT CAVOK 24/13 Q1015 NOSIG"
            );
        }
        if (city.Contains("fethiye"))
        {
            return new AirportInflowSummary(
                "DLM",
                "Dalaman Uluslararası Havalimanı (DLM)",
                185,
                29800,
                95.2,
                16,
                16.9,
                ActiveRunway: "Pist 01 (ILS CAT I)",
                WindCondition: "010° / 09 kt (Kuzey)",
                Visibility: "10+ km (CAVOK)",
                Metar: "METAR LTBS 160820Z 01009KT CAVOK 27/15 Q1013 NOSIG"
            );
        }
        if (city.Contains("paris"))
        {
            return new AirportInflowSummary(
                "CDG",
                "Paris Charles de Gaulle & Orly (CDG/ORY)",
                1450,
                218000,
                91.8,
                62,
                9.4,
                ActiveRunway: "RWY 27L/27R (ILS CAT III)",
                WindCondition: "250° / 14 kt (Batı)",
                Visibility: "10+ km (CAVOK)",
                Metar: "METAR LFPG 160820Z 25014KT CAVOK 18/10 Q1016 NOSIG"
            );
        }
        if (city.Contains("dubai"))
        {
            return new AirportInflowSummary(
                "DXB",
                "Dubai International Hub (DXB)",
                1120,
                238500,
                95.4,
                85,
                24.1,
                ActiveRunway: "RWY 30L/30R (ILS CAT IIIB)",
                WindCondition: "320° / 10 kt (Kuzeybatı)",
                Visibility: "9000 m (HAZE)",
                Metar: "METAR OMDB 160820Z 32010KT 9000 NSC 34/21 Q1008 NOSIG"
            );
        }

        return new AirportInflowSummary(
            "HTL",
            $"{hotel.City} Bölgesel Havalimanı",
            120,
            18400,
            91.0,
            12,
            10.0,
            ActiveRunway: "Pist 36 (ILS CAT I)",
            WindCondition: "360° / 08 kt",
            Visibility: "10 km (CAVOK)",
            Metar: "METAR LTXX 160820Z 36008KT CAVOK 23/12 Q1015 NOSIG"
        );
    }

    public List<FlightInboundInfo> GetArrivingFlights(Hotel hotel)
    {
        var city = hotel.City.ToLowerInvariant();
        List<FlightInboundInfo> list;
        if (city.Contains("bodrum"))
        {
            list = new List<FlightInboundInfo>
            {
                new(
                    FlightNumber: "TK 2144",
                    OriginCity: "Londra Gatwick",
                    OriginCountry: "İngiltere",
                    OriginFlag: "🇬🇧",
                    ArrivalTime: "14:15",
                    AircraftType: "Boeing 737 MAX 9",
                    Capacity: 189,
                    LoadFactorPercent: 98,
                    Status: "İndi",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>
                    {
                        new("Zeynep Kaya", "204", "Deluxe Süit", "#REZ-8491", "VIP Misafir", "VIP Vito Bekliyor", "Ahmet Usta (34 VIP 007)", "15:05 (~35 dk sonra)", "+90 532 411 22 33",
                            PickupSignText: "Sayın Zeynep KAYA - OtelPro Resort & Spa", TerminalGate: "VIP Çıkış Kapısı 2", VehiclePlate: "34 VIP 007", VehicleModel: "Mercedes-Maybach Vito Exclusive", BaggageStatus: "Bavul Bandı 3'te Alındı", RoomReadiness: "✅ Oda Hazırlandı & Havalandırıldı", SpecialWishes: "🍾 Karşılama şampanyası & taze meyve sepeti hazır"),
                        new("Marcus Lindqvist", "302", "Deniz Manzaralı", "#REZ-8492", "Gold Üye", "Bagaj Bandı 3'te", "Havalimanı Servisi 1", "15:15 (~45 dk sonra)", "+44 7700 900123",
                            PickupSignText: "Mr. Marcus LINDQVIST - OtelPro", TerminalGate: "Dış Hatlar Genel Çıkış 1", VehiclePlate: "34 HTL 101", VehicleModel: "Mercedes Sprinter VIP Line", BaggageStatus: "Bavul Bekleniyor", RoomReadiness: "✅ Oda Temizlendi & Denetlendi", SpecialWishes: "🌊 Yüksek kat deniz manzarası tercihi")
                    },
                    AirlineName: "Türk Hava Yolları",
                    AirlineCode: "TK",
                    TailNumber: "TC-JSE",
                    AltitudeFt: 2400,
                    SpeedKnots: 185,
                    HeadingDeg: 345,
                    DistanceNm: 8.4,
                    Runway: "Pist 36R",
                    Gate: "Körük 104",
                    BaggageBelt: "Bant 3",
                    StatusDetail: "Final Yaklaşmada · ILS Kilitlendi",
                    RadarX: 52.0,
                    RadarY: 42.0
                ),
                new(
                    FlightNumber: "LH 1302",
                    OriginCity: "Frankfurt Main",
                    OriginCountry: "Almanya",
                    OriginFlag: "🇩🇪",
                    ArrivalTime: "14:40",
                    AircraftType: "Airbus A321neo",
                    Capacity: 215,
                    LoadFactorPercent: 96,
                    Status: "Yaklaşıyor",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>
                    {
                        new("Dr. Hans Becker", "401", "Presidential Süit", "#REZ-8504", "Ultra VIP", "Uçuşta / Yaklaşıyor", "Mehmet K. (34 VIP 102)", "15:30 (~50 dk sonra)", "+49 151 23456789",
                            PickupSignText: "Dr. Hans BECKER - OtelPro Presidential Suite", TerminalGate: "VIP Terminal Çıkışı", VehiclePlate: "34 VIP 102", VehicleModel: "BMW 7 Serisi Protection", BaggageStatus: "Öncelikli Diplomatik Bagaj", RoomReadiness: "✅ Presidential Süit Karşılama Hazır", SpecialWishes: "🍷 Bordo kırmızı şarabı & puro tabağı")
                    },
                    AirlineName: "Lufthansa",
                    AirlineCode: "LH",
                    TailNumber: "D-AIEQ",
                    AltitudeFt: 6500,
                    SpeedKnots: 220,
                    HeadingDeg: 310,
                    DistanceNm: 22.1,
                    Runway: "Pist 36R",
                    Gate: "Körük 102",
                    BaggageBelt: "Bant 2",
                    StatusDetail: "Alçalma Fazında · Radar Vektörü",
                    RadarX: 66.0,
                    RadarY: 28.0
                ),
                new(
                    FlightNumber: "PC 2280",
                    OriginCity: "İstanbul Sabiha",
                    OriginCountry: "Türkiye",
                    OriginFlag: "🇹🇷",
                    ArrivalTime: "15:05",
                    AircraftType: "Airbus A320neo",
                    Capacity: 186,
                    LoadFactorPercent: 99,
                    Status: "Zamanında",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>
                    {
                        new("Ayşe & Emre Demir", "108", "Standart Oda", "#REZ-8512", "Balayı Çifti", "Zamanında / Uçuşta", "Otel Transfer Aracı 2", "15:55 (~50 dk sonra)", "+90 533 555 88 99",
                            PickupSignText: "Ayşe & Emre DEMİR - OtelPro Hoş Geldiniz", TerminalGate: "İç Hatlar Çıkışı", VehiclePlate: "34 TRF 55", VehicleModel: "Volkswagen Caravelle VIP", BaggageStatus: "Uçakta Yüklü", RoomReadiness: "✅ Balayı Yatak Süslemesi & Pasta Hazır", SpecialWishes: "🍓 Çikolata kaplı çilek & gül yaprakları")
                    },
                    AirlineName: "Pegasus Airlines",
                    AirlineCode: "PC",
                    TailNumber: "TC-NBC",
                    AltitudeFt: 11200,
                    SpeedKnots: 275,
                    HeadingDeg: 355,
                    DistanceNm: 36.4,
                    Runway: "Pist 36R",
                    Gate: "Körük 106",
                    BaggageBelt: "Bant 4",
                    StatusDetail: "Hava Sahasında · Yaklaşma Sırasında",
                    RadarX: 48.0,
                    RadarY: 66.0
                ),
                new(
                    FlightNumber: "EK 121",
                    OriginCity: "Dubai Al Maktoum",
                    OriginCountry: "BAE",
                    OriginFlag: "🇦🇪",
                    ArrivalTime: "15:35",
                    AircraftType: "Boeing 777-300ER",
                    Capacity: 354,
                    LoadFactorPercent: 94,
                    Status: "Zamanında",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>
                    {
                        new("Rashid Al-Nuaimi", "305", "King Deluxe", "#REZ-8526", "VIP Misafir", "Zamanında / Uçuşta", "VIP Mercedes Vito 3", "16:25 (~50 dk sonra)", "+971 50 998 7766",
                            PickupSignText: "Mr. Rashid AL-NUAIMI - OtelPro VIP Host", TerminalGate: "Dış Hatlar VIP Kapı", VehiclePlate: "34 VIP 003", VehicleModel: "Mercedes-Maybach V-Class", BaggageStatus: "First Class Priority Tag", RoomReadiness: "✅ Odaya Gurme Karşılama İkramı", SpecialWishes: "☕ Geleneksel Arap kahvesi & Medine hurması")
                    },
                    AirlineName: "Emirates",
                    AirlineCode: "EK",
                    TailNumber: "A6-EPU",
                    AltitudeFt: 18500,
                    SpeedKnots: 320,
                    HeadingDeg: 290,
                    DistanceNm: 58.0,
                    Runway: "Pist 36R",
                    Gate: "Körük 108",
                    BaggageBelt: "Bant 1",
                    StatusDetail: "Seyir Fazında · Giriş Raporlandı",
                    RadarX: 82.0,
                    RadarY: 68.0
                ),
                new(
                    FlightNumber: "AF 1890",
                    OriginCity: "Paris Charles de Gaulle",
                    OriginCountry: "Fransa",
                    OriginFlag: "🇫🇷",
                    ArrivalTime: "16:10",
                    AircraftType: "Airbus A321neo",
                    Capacity: 220,
                    LoadFactorPercent: 95,
                    Status: "Zamanında",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>
                    {
                        new("Camille Dubois", "210", "Deniz Manzaralı", "#REZ-8535", "Direct Booking", "Uçuşta", "Ön Büro Transferi", "17:00 (~50 dk sonra)", "+33 6 12 34 56 78",
                            PickupSignText: "Mme. Camille DUBOIS - Bienvenue OtelPro", TerminalGate: "Dış Hatlar A Kapısı", VehiclePlate: "34 HTL 204", VehicleModel: "Mercedes Vito Tourer", BaggageStatus: "Uçakta", RoomReadiness: "✅ Hazır", SpecialWishes: "Lavanta oda kokusu buketi")
                    },
                    AirlineName: "Air France",
                    AirlineCode: "AF",
                    TailNumber: "F-HPJA",
                    AltitudeFt: 22000,
                    SpeedKnots: 350,
                    HeadingDeg: 140,
                    DistanceNm: 74.0,
                    Runway: "Pist 36R",
                    Gate: "Körük 103",
                    BaggageBelt: "Bant 2",
                    StatusDetail: "Hava Sahası Girişinde",
                    RadarX: 22.0,
                    RadarY: 30.0
                ),
                new(
                    FlightNumber: "LX 1804",
                    OriginCity: "Zürih Kloten",
                    OriginCountry: "İsviçre",
                    OriginFlag: "🇨🇭",
                    ArrivalTime: "16:45",
                    AircraftType: "Airbus A220-300",
                    Capacity: 145,
                    LoadFactorPercent: 93,
                    Status: "Planlandı",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>(),
                    AirlineName: "Swiss International",
                    AirlineCode: "LX",
                    TailNumber: "HB-JCA",
                    AltitudeFt: 28000,
                    SpeedKnots: 380,
                    HeadingDeg: 160,
                    DistanceNm: 88.0,
                    Runway: "Pist 36R",
                    Gate: "Körük 105",
                    BaggageBelt: "Bant 3",
                    StatusDetail: "Planlanan Rota Üzerinde",
                    RadarX: 18.0,
                    RadarY: 76.0
                ),
                new(
                    FlightNumber: "VIP 902",
                    OriginCity: "Cenevre VIP Terminali",
                    OriginCountry: "İsviçre",
                    OriginFlag: "🇨🇭",
                    ArrivalTime: "17:10",
                    AircraftType: "Gulfstream G650ER",
                    Capacity: 14,
                    LoadFactorPercent: 100,
                    Status: "Özel Jet",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>
                    {
                        new("Sheikh Tariq Al-Mansoor", "410", "Kral Dairesi", "#REZ-8550", "Ultra VIP / Diplomat", "VIP Vito Pist Yanında", "Özel Protokol: Can B. (34 VIP 999)", "17:45 (~35 dk sonra)", "+971 50 123 4567",
                            PickupSignText: "H.E. Sheikh Tariq AL-MANSOOR - OtelPro Royal Suite", TerminalGate: "Genel Havacılık VIP Apronu", VehiclePlate: "34 VIP 999", VehicleModel: "Rolls-Royce Phantom / Maybach Konvoy", BaggageStatus: "Doğrudan Araç Konvoyuna Aktarım", RoomReadiness: "✅ Kral Dairesi 24 Saat Özel Butler Servisi Aktif", SpecialWishes: "👑 Özel güvenlik koruması & diplomatik karşılama")
                    },
                    AirlineName: "VIP Business Aviation",
                    AirlineCode: "VIP",
                    TailNumber: "TC-VIP",
                    AltitudeFt: 3800,
                    SpeedKnots: 195,
                    HeadingDeg: 340,
                    DistanceNm: 12.6,
                    Runway: "Pist 36R",
                    Gate: "VIP Apron Park 1",
                    BaggageBelt: "VIP Protokol",
                    StatusDetail: "VIP Yaklaşma · Pist Yanı Karşılama İzni",
                    RadarX: 56.0,
                    RadarY: 36.0
                )
            };
        }
        else if (city.Contains("istanbul"))
        {
            list = new List<FlightInboundInfo>
            {
                new(
                    FlightNumber: "TK 0001",
                    OriginCity: "New York JFK",
                    OriginCountry: "ABD",
                    OriginFlag: "🇺🇸",
                    ArrivalTime: "13:45",
                    AircraftType: "Boeing 777-300ER",
                    Capacity: 349,
                    LoadFactorPercent: 97,
                    Status: "İndi",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>
                    {
                        new("Prof. Michael Stone", "501", "Boğaz Süit", "#REZ-9102", "VIP Misafir", "İndi / Pasaport Kontrolünde", "Şoför: Sinan K. (34 BOGAZ 01)", "14:45 (~40 dk sonra)", "+1 212 555 0199",
                            PickupSignText: "Prof. Michael STONE - OtelPro Bosphorus", TerminalGate: "Dış Hatlar VIP Kapı", VehiclePlate: "34 BOGAZ 01", VehicleModel: "Mercedes S-Class 400d", BaggageStatus: "Bant 12'den Alındı", RoomReadiness: "✅ Boğaz Süit Hazır", SpecialWishes: "Boğaz Manzaralı Balkon Çiçekleri"),
                        new("Sarah Jenkins", "304", "Deluxe King", "#REZ-9103", "İş Seyahati", "İndi / Bagaj Bekliyor", "VIP Transfer Vito", "14:55 (~50 dk sonra)", "+1 415 555 0188",
                            PickupSignText: "Ms. Sarah JENKINS - OtelPro", TerminalGate: "Dış Hatlar Kapı 4", VehiclePlate: "34 VIP 110", VehicleModel: "Mercedes Vito VIP", BaggageStatus: "Bant 12'de", RoomReadiness: "✅ Hazır", SpecialWishes: "Hızlı Wi-Fi ve Çalışma Masası")
                    },
                    AirlineName: "Türk Hava Yolları",
                    AirlineCode: "TK",
                    TailNumber: "TC-LJB",
                    AltitudeFt: 1800,
                    SpeedKnots: 170,
                    HeadingDeg: 350,
                    DistanceNm: 6.2,
                    Runway: "Pist 35R",
                    Gate: "Körük F4",
                    BaggageBelt: "Bant 12",
                    StatusDetail: "Piste İndi · Taksi Yolunda",
                    RadarX: 51.0,
                    RadarY: 48.0
                ),
                new(
                    FlightNumber: "BA 676",
                    OriginCity: "Londra Heathrow",
                    OriginCountry: "İngiltere",
                    OriginFlag: "🇬🇧",
                    ArrivalTime: "14:20",
                    AircraftType: "Airbus A350-900",
                    Capacity: 315,
                    LoadFactorPercent: 95,
                    Status: "İndi",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>
                    {
                        new("Sir Arthur Pendelton", "601", "Boğaz Palace Süit", "#REZ-9115", "Ultra VIP", "İndi / VIP Lounge Karşılama", "VIP Maybach: Ercan T. (34 VIP 001)", "15:15 (~45 dk sonra)", "+44 20 7946 0991",
                            PickupSignText: "Sir Arthur PENDELTON - OtelPro Palace", TerminalGate: "Genel Havacılık VIP Lounge", VehiclePlate: "34 VIP 001", VehicleModel: "Mercedes-Maybach S680", BaggageStatus: "VIP Taşıma", RoomReadiness: "✅ Özel Şampanya & Karşılama Ekibi Hazır", SpecialWishes: "Piyano Odası Tahsisi")
                    },
                    AirlineName: "British Airways",
                    AirlineCode: "BA",
                    TailNumber: "G-XWBA",
                    AltitudeFt: 3200,
                    SpeedKnots: 190,
                    HeadingDeg: 345,
                    DistanceNm: 10.5,
                    Runway: "Pist 35L",
                    Gate: "Körük D2",
                    BaggageBelt: "Bant 8",
                    StatusDetail: "Son Yaklaşma · Kule Yetkisi Alındı",
                    RadarX: 53.0,
                    RadarY: 38.0
                ),
                new(
                    FlightNumber: "QR 241",
                    OriginCity: "Doha Hamad",
                    OriginCountry: "Katar",
                    OriginFlag: "🇶🇦",
                    ArrivalTime: "14:55",
                    AircraftType: "Boeing 787-9 Dreamliner",
                    Capacity: 298,
                    LoadFactorPercent: 98,
                    Status: "Yaklaşıyor",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>
                    {
                        new("Fatima Al-Thani", "402", "Executive Suite", "#REZ-9128", "VIP Misafir", "Yaklaşıyor", "VIP Vito: Orhan Y. (34 VIP 044)", "15:45 (~50 dk sonra)", "+974 33 123 456",
                            PickupSignText: "Mrs. Fatima AL-THANI - Welcome OtelPro", TerminalGate: "VIP Kapı 1", VehiclePlate: "34 VIP 044", VehicleModel: "Mercedes-Benz V-Class Avantgarde", BaggageStatus: "Uçakta", RoomReadiness: "✅ Executive Katı Hazır", SpecialWishes: "Geleneksel Türk Lokumu & Çay Seti")
                    },
                    AirlineName: "Qatar Airways",
                    AirlineCode: "QR",
                    TailNumber: "A7-BHA",
                    AltitudeFt: 8400,
                    SpeedKnots: 245,
                    HeadingDeg: 330,
                    DistanceNm: 26.0,
                    Runway: "Pist 35R",
                    Gate: "Körük E1",
                    BaggageBelt: "Bant 14",
                    StatusDetail: "Alçalma Kontrolünde",
                    RadarX: 68.0,
                    RadarY: 62.0
                ),
                new(
                    FlightNumber: "LH 1298",
                    OriginCity: "Münih Franz Josef",
                    OriginCountry: "Almanya",
                    OriginFlag: "🇩🇪",
                    ArrivalTime: "15:20",
                    AircraftType: "Airbus A321neo",
                    Capacity: 215,
                    LoadFactorPercent: 94,
                    Status: "Zamanında",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>
                    {
                        new("Maximilian Weber", "208", "Deluxe Boğaz", "#REZ-9134", "Kongre Delegesi", "Zamanında", "Otel Kongre Shuttle", "16:15 (~55 dk sonra)", "+49 89 987654",
                            PickupSignText: "Mr. Maximilian WEBER - Medtech Kongre", TerminalGate: "Dış Hatlar Çıkışı", VehiclePlate: "34 SHT 88", VehicleModel: "Mercedes Sprinter Executive", BaggageStatus: "Bekleniyor", RoomReadiness: "✅ Hazır", SpecialWishes: "Kongre Yaka Kartı Odaya Bırakılsın")
                    },
                    AirlineName: "Lufthansa",
                    AirlineCode: "LH",
                    TailNumber: "D-AIEU",
                    AltitudeFt: 14500,
                    SpeedKnots: 290,
                    HeadingDeg: 120,
                    DistanceNm: 44.0,
                    Runway: "Pist 35L",
                    Gate: "Körük D7",
                    BaggageBelt: "Bant 6",
                    StatusDetail: "Hava Trafik Sıralamasında",
                    RadarX: 32.0,
                    RadarY: 24.0
                ),
                new(
                    FlightNumber: "EK 123",
                    OriginCity: "Dubai International",
                    OriginCountry: "BAE",
                    OriginFlag: "🇦🇪",
                    ArrivalTime: "15:50",
                    AircraftType: "Airbus A380-800",
                    Capacity: 516,
                    LoadFactorPercent: 96,
                    Status: "Zamanında",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>(),
                    AirlineName: "Emirates",
                    AirlineCode: "EK",
                    TailNumber: "A6-EVK",
                    AltitudeFt: 22000,
                    SpeedKnots: 340,
                    HeadingDeg: 300,
                    DistanceNm: 68.0,
                    Runway: "Pist 35R",
                    Gate: "Körük F1",
                    BaggageBelt: "Bant 1",
                    StatusDetail: "Seyir Fazında",
                    RadarX: 79.0,
                    RadarY: 72.0
                ),
                new(
                    FlightNumber: "TK 0050",
                    OriginCity: "Tokyo Haneda",
                    OriginCountry: "Japonya",
                    OriginFlag: "🇯🇵",
                    ArrivalTime: "16:30",
                    AircraftType: "Boeing 787-9 Dreamliner",
                    Capacity: 300,
                    LoadFactorPercent: 92,
                    Status: "Zamanında",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>(),
                    AirlineName: "Türk Hava Yolları",
                    AirlineCode: "TK",
                    TailNumber: "TC-LLC",
                    AltitudeFt: 26000,
                    SpeedKnots: 370,
                    HeadingDeg: 260,
                    DistanceNm: 84.0,
                    Runway: "Pist 35R",
                    Gate: "Körük E4",
                    BaggageBelt: "Bant 10",
                    StatusDetail: "Sektör Kontrolünde",
                    RadarX: 86.0,
                    RadarY: 34.0
                )
            };
        }
        else if (city.Contains("antalya"))
        {
            list = new List<FlightInboundInfo>
            {
                new(
                    FlightNumber: "XQ 172",
                    OriginCity: "Berlin Brandenburg",
                    OriginCountry: "Almanya",
                    OriginFlag: "🇩🇪",
                    ArrivalTime: "13:50",
                    AircraftType: "Boeing 737-800",
                    Capacity: 189,
                    LoadFactorPercent: 98,
                    Status: "İndi",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>
                    {
                        new("Klaus & Helga Schneider", "210", "Aile Odası", "#REZ-7201", "Tekrar Eden Misafir (12. Konaklama)", "İndi / Transferde", "Otel Shuttle 1", "14:35 (~30 dk sonra)", "+49 30 123456",
                            PickupSignText: "Klaus & Helga SCHNEIDER - Hoş Geldiniz", TerminalGate: "1 Nolu Dış Hatlar Çıkışı", VehiclePlate: "07 AYT 44", VehicleModel: "Mercedes Vito VIP", BaggageStatus: "Alındı", RoomReadiness: "✅ Aile Odası Hazır", SpecialWishes: "Balkonda Çocuk Güvenlik Kilidi"),
                        new("Elena Petrova", "315", "Deluxe Deniz", "#REZ-7204", "All Inclusive VIP", "Bagaj Alımında", "VIP Minibüs", "14:45 (~40 dk sonra)", "+7 916 123 4567",
                            PickupSignText: "Elena PETROVA - OtelPro Resort", TerminalGate: "1 Nolu Dış Hatlar", VehiclePlate: "07 VIP 100", VehicleModel: "Mercedes Vito Exclusive", BaggageStatus: "Bant 4'te", RoomReadiness: "✅ Deniz Manzaralı Oda Hazır", SpecialWishes: "Meyve Tabağı & Şarap")
                    },
                    AirlineName: "SunExpress",
                    AirlineCode: "XQ",
                    TailNumber: "TC-SOA",
                    AltitudeFt: 2100,
                    SpeedKnots: 175,
                    HeadingDeg: 360,
                    DistanceNm: 7.2,
                    Runway: "Pist 36C",
                    Gate: "Körük 12",
                    BaggageBelt: "Bant 4",
                    StatusDetail: "İndi · Park Pozisyonunda",
                    RadarX: 50.0,
                    RadarY: 46.0
                ),
                new(
                    FlightNumber: "SU 2142",
                    OriginCity: "Moskova Sheremetyevo",
                    OriginCountry: "Rusya",
                    OriginFlag: "🇷🇺",
                    ArrivalTime: "14:25",
                    AircraftType: "Airbus A330-300",
                    Capacity: 296,
                    LoadFactorPercent: 97,
                    Status: "Yaklaşıyor",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>
                    {
                        new("Dmitry Ivanov", "405", "Villa Suite", "#REZ-7212", "VIP Misafir", "Yaklaşıyor", "Özel Şoför: Serkan B.", "15:15 (~35 dk sonra)", "+7 495 999 8877",
                            PickupSignText: "Mr. Dmitry IVANOV - OtelPro Villa", TerminalGate: "VIP Kapı 2", VehiclePlate: "07 VIP 777", VehicleModel: "Mercedes Maybach Vito", BaggageStatus: "Uçakta", RoomReadiness: "✅ Özel Havuzlu Villa Hazır", SpecialWishes: "Havuz Isıtması Açıldı")
                    },
                    AirlineName: "Aeroflot",
                    AirlineCode: "SU",
                    TailNumber: "RA-73700",
                    AltitudeFt: 5800,
                    SpeedKnots: 215,
                    HeadingDeg: 350,
                    DistanceNm: 18.5,
                    Runway: "Pist 36C",
                    Gate: "Körük 16",
                    BaggageBelt: "Bant 6",
                    StatusDetail: "Son Yaklaşma Sektöründe",
                    RadarX: 52.0,
                    RadarY: 32.0
                ),
                new(
                    FlightNumber: "TK 2412",
                    OriginCity: "Londra Gatwick",
                    OriginCountry: "İngiltere",
                    OriginFlag: "🇬🇧",
                    ArrivalTime: "15:00",
                    AircraftType: "Airbus A321neo",
                    Capacity: 215,
                    LoadFactorPercent: 96,
                    Status: "Zamanında",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>
                    {
                        new("David & Claire Wilson", "502", "Balayı Süiti", "#REZ-7218", "Balayı Çifti", "Zamanında / Uçuşta", "Şampanyalı Özel Karşılama: Ali D.", "15:45 (~45 dk sonra)", "+44 7911 123456",
                            PickupSignText: "David & Claire WILSON - Balayı Karşılama", TerminalGate: "Dış Hatlar Kapı 1", VehiclePlate: "07 VIP 303", VehicleModel: "Mercedes Vito VIP", BaggageStatus: "Uçakta", RoomReadiness: "✅ Balayı İkramları Hazırlandı", SpecialWishes: "Şampanya & Gül Yaprakları")
                    },
                    AirlineName: "Türk Hava Yolları",
                    AirlineCode: "TK",
                    TailNumber: "TC-LSD",
                    AltitudeFt: 12000,
                    SpeedKnots: 280,
                    HeadingDeg: 340,
                    DistanceNm: 38.0,
                    Runway: "Pist 36C",
                    Gate: "Körük 14",
                    BaggageBelt: "Bant 2",
                    StatusDetail: "Alçalma Rotasında",
                    RadarX: 56.0,
                    RadarY: 68.0
                ),
                new(
                    FlightNumber: "W6 3120",
                    OriginCity: "Varşova Chopin",
                    OriginCountry: "Polonya",
                    OriginFlag: "🇵🇱",
                    ArrivalTime: "15:30",
                    AircraftType: "Airbus A321neo",
                    Capacity: 239,
                    LoadFactorPercent: 95,
                    Status: "Zamanında",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>(),
                    AirlineName: "Wizz Air",
                    AirlineCode: "W6",
                    TailNumber: "HA-LVI",
                    AltitudeFt: 19500,
                    SpeedKnots: 310,
                    HeadingDeg: 170,
                    DistanceNm: 62.0,
                    Runway: "Pist 36C",
                    Gate: "Körük 10",
                    BaggageBelt: "Bant 3",
                    StatusDetail: "Seyir Halinde",
                    RadarX: 25.0,
                    RadarY: 26.0
                ),
                new(
                    FlightNumber: "TK 2420",
                    OriginCity: "İstanbul Havalimanı",
                    OriginCountry: "Türkiye",
                    OriginFlag: "🇹🇷",
                    ArrivalTime: "16:00",
                    AircraftType: "Airbus A330-200",
                    Capacity: 250,
                    LoadFactorPercent: 99,
                    Status: "Zamanında",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>(),
                    AirlineName: "Türk Hava Yolları",
                    AirlineCode: "TK",
                    TailNumber: "TC-JND",
                    AltitudeFt: 25000,
                    SpeedKnots: 360,
                    HeadingDeg: 190,
                    DistanceNm: 78.0,
                    Runway: "Pist 36C",
                    Gate: "Körük 18",
                    BaggageBelt: "Bant 5",
                    StatusDetail: "Hava Sahası Girişinde",
                    RadarX: 30.0,
                    RadarY: 78.0
                )
            };
        }
        else
        {
            list = new List<FlightInboundInfo>
            {
                new(
                    FlightNumber: "TK 2304",
                    OriginCity: "İstanbul Havalimanı",
                    OriginCountry: "Türkiye",
                    OriginFlag: "🇹🇷",
                    ArrivalTime: "14:00",
                    AircraftType: "Airbus A321neo",
                    Capacity: 210,
                    LoadFactorPercent: 95,
                    Status: "İndi",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>
                    {
                        new("Ahmet Yılmaz", "102", "Standart Oda", "#REZ-6010", "Direkt Rezervasyon", "İndi / Transferde", "Otel Servisi", "14:45 (~45 dk sonra)", "+90 532 111 22 33",
                            PickupSignText: "Sayın Ahmet YILMAZ - Hoş Geldiniz", TerminalGate: "İç Hatlar Çıkışı", VehiclePlate: "34 TRF 01", VehicleModel: "Mercedes Vito", BaggageStatus: "Alındı", RoomReadiness: "✅ Hazır", SpecialWishes: "Sessiz Oda")
                    },
                    AirlineName: "Türk Hava Yolları",
                    AirlineCode: "TK",
                    TailNumber: "TC-JSH",
                    AltitudeFt: 2200,
                    SpeedKnots: 180,
                    HeadingDeg: 350,
                    DistanceNm: 7.5,
                    Runway: "Pist 36",
                    Gate: "Kapı 2",
                    BaggageBelt: "Bant 1",
                    StatusDetail: "İndi · Terminale Yanaştı",
                    RadarX: 51.0,
                    RadarY: 45.0
                ),
                new(
                    FlightNumber: "PC 2812",
                    OriginCity: "Sabiha Gökçen",
                    OriginCountry: "Türkiye",
                    OriginFlag: "🇹🇷",
                    ArrivalTime: "14:45",
                    AircraftType: "Airbus A320neo",
                    Capacity: 186,
                    LoadFactorPercent: 94,
                    Status: "Yaklaşıyor",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>(),
                    AirlineName: "Pegasus Airlines",
                    AirlineCode: "PC",
                    TailNumber: "TC-NCR",
                    AltitudeFt: 6200,
                    SpeedKnots: 220,
                    HeadingDeg: 340,
                    DistanceNm: 20.0,
                    Runway: "Pist 36",
                    Gate: "Kapı 3",
                    BaggageBelt: "Bant 2",
                    StatusDetail: "Yaklaşma Kontrolünde",
                    RadarX: 58.0,
                    RadarY: 34.0
                ),
                new(
                    FlightNumber: "LH 1840",
                    OriginCity: "Frankfurt Main",
                    OriginCountry: "Almanya",
                    OriginFlag: "🇩🇪",
                    ArrivalTime: "15:30",
                    AircraftType: "Airbus A321",
                    Capacity: 200,
                    LoadFactorPercent: 92,
                    Status: "Zamanında",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>(),
                    AirlineName: "Lufthansa",
                    AirlineCode: "LH",
                    TailNumber: "D-AIRY",
                    AltitudeFt: 16000,
                    SpeedKnots: 300,
                    HeadingDeg: 130,
                    DistanceNm: 52.0,
                    Runway: "Pist 36",
                    Gate: "Kapı 1",
                    BaggageBelt: "Bant 1",
                    StatusDetail: "Alçalma Sırasında",
                    RadarX: 28.0,
                    RadarY: 28.0
                ),
                new(
                    FlightNumber: "BA 690",
                    OriginCity: "Londra Heathrow",
                    OriginCountry: "İngiltere",
                    OriginFlag: "🇬🇧",
                    ArrivalTime: "16:15",
                    AircraftType: "Boeing 737",
                    Capacity: 175,
                    LoadFactorPercent: 91,
                    Status: "Zamanında",
                    ArrivingGuests: new List<ArrivingGuestFlightInfo>(),
                    AirlineName: "British Airways",
                    AirlineCode: "BA",
                    TailNumber: "G-DOCB",
                    AltitudeFt: 24000,
                    SpeedKnots: 350,
                    HeadingDeg: 150,
                    DistanceNm: 76.0,
                    Runway: "Pist 36",
                    Gate: "Kapı 4",
                    BaggageBelt: "Bant 2",
                    StatusDetail: "Seyir Rotasında",
                    RadarX: 20.0,
                    RadarY: 72.0
                )
            };
        }

        // Uçuş bilgilerini misafir kayıtlarına otomatik ekle
        foreach (var f in list)
        {
            for (int i = 0; i < f.ArrivingGuests.Count; i++)
            {
                var g = f.ArrivingGuests[i];
                if (string.IsNullOrEmpty(g.FlightNumber))
                {
                    f.ArrivingGuests[i] = g with
                    {
                        FlightNumber = f.FlightNumber,
                        OriginCity = f.OriginCity,
                        OriginCountry = f.OriginCountry,
                        OriginFlag = f.OriginFlag,
                        FlightStatus = f.Status,
                        ArrivalTime = f.ArrivalTime
                    };
                }
            }
        }

        return list;
    }

    public ArrivingGuestFlightInfo GetFlightForReservation(Reservation res, Hotel? hotel = null)
    {
        var guestName = res.Guest?.FullName ?? "";
        var code = res.Code ?? "";

        if (hotel != null)
        {
            var flights = GetArrivingFlights(hotel);
            var match = flights.SelectMany(f => f.ArrivingGuests)
                .FirstOrDefault(g => (!string.IsNullOrEmpty(code) && g.ReservationCode.Contains(code)) ||
                                     (!string.IsNullOrEmpty(guestName) && g.GuestName.Equals(guestName, StringComparison.OrdinalIgnoreCase)));
            if (match != null) return match;
        }

        // Tesis veya misafir profiline göre tutarlı uçuş telemetrisi üret
        var room = res.Rooms.FirstOrDefault()?.Room?.Number ?? "104";
        var roomType = res.Rooms.FirstOrDefault()?.RoomType?.Name ?? "Standart Oda";
        var nat = res.Guest?.Nationality ?? "TR";
        var hash = Math.Abs((res.Id * 397) ^ (res.GuestId * 31));

        var flightPick = (nat, hash % 4) switch
        {
            ("DE", 0) => ("LH 1302", "Frankfurt Main", "Almanya", "🇩🇪", "14:40", "Yaklaşıyor", "Havalimanı Karşılama Ekibi Hazır"),
            ("DE", _) => ("XQ 172", "Berlin Brandenburg", "Almanya", "🇩🇪", "13:50", "İndi", "VIP Minibüs Bekliyor"),
            ("GB", 0) => ("BA 676", "Londra Heathrow", "İngiltere", "🇬🇧", "14:20", "İndi", "VIP Vito Pist Yanında"),
            ("GB", _) => ("TK 2144", "Londra Gatwick", "İngiltere", "🇬🇧", "14:15", "İndi", "Şoför: Ahmet Usta (34 VIP 007)"),
            ("RU", _) => ("SU 2142", "Moskova Sheremetyevo", "Rusya", "🇷🇺", "14:25", "Yaklaşıyor", "Özel Şoför: Serkan B."),
            ("FR", _) => ("AF 1890", "Paris Charles de Gaulle", "Fransa", "🇫🇷", "16:10", "Zamanında", "Ön Büro Transferi"),
            ("TR", 0) => ("TK 2304", "İstanbul Havalimanı", "Türkiye", "🇹🇷", "14:00", "İndi", "Otel Shuttle Servisi"),
            ("TR", 1) => ("PC 2280", "İstanbul Sabiha", "Türkiye", "🇹🇷", "15:05", "Zamanında", "Otel Transfer Aracı 2"),
            ("TR", 2) => ("PC 2812", "İzmir Adnan Menderes", "Türkiye", "🇹🇷", "14:45", "Yaklaşıyor", "Özel Karşılama Ekibi"),
            _ => ("TK 0001", "New York JFK", "ABD", "🇺🇸", "13:45", "İndi", "VIP Vito Transfer")
        };

        var driverPick = (hash % 3) switch
        {
            0 => ("Ahmet Usta (34 VIP 007 - Mercedes Vito)", "15:05 (~30 dk sonra)"),
            1 => ("Can B. (34 VIP 999 - Özel Protokol)", "15:25 (~45 dk sonra)"),
            _ => ("Orhan Y. (34 VIP 044 - VIP Shuttle)", "15:40 (~55 dk sonra)")
        };

        return new ArrivingGuestFlightInfo(
            GuestName: string.IsNullOrWhiteSpace(guestName) ? "Misafir" : guestName,
            RoomNumber: room,
            RoomType: roomType,
            ReservationCode: string.IsNullOrEmpty(code) ? $"#RZ{res.Id}" : code,
            VipStatus: res.Guest?.IsVip == true ? "VIP Misafir" : "Onaylı Misafir",
            TransferStatus: flightPick.Item7,
            DriverInfo: driverPick.Item1,
            EstimatedHotelArrival: driverPick.Item2,
            Phone: res.Guest?.Phone ?? "+90 532 000 00 00",
            FlightNumber: flightPick.Item1,
            OriginCity: flightPick.Item2,
            OriginCountry: flightPick.Item3,
            OriginFlag: flightPick.Item4,
            FlightStatus: flightPick.Item6,
            ArrivalTime: flightPick.Item5
        );
    }

    public List<ArrivingGuestFlightInfo> GetAllArrivingGuests(Hotel hotel)
    {
        return GetArrivingFlights(hotel).SelectMany(f => f.ArrivingGuests).ToList();
    }

    public List<CityRadarEvent> GetCityEvents(Hotel hotel)
    {
        var city = hotel.City.ToLowerInvariant();
        var today = DateTime.Today;
        var list = new List<CityRadarEvent>();

        if (city.Contains("bodrum"))
        {
            list.AddRange(new[]
            {
                new CityRadarEvent(101, hotel.Id, "Megastar Tarkan Büyük Açıkhava Konseri", "Concert", "🎵", "#ec4899", "Bodrum Antik Tiyatrosu", 3.2, today.AddDays(2), 5200, 100, 96, "Yaz sezonunun en büyük müzik olayı. Biletler dakikalar içinde tükendi. Otelimize 3.2 km mesafede."),
                new CityRadarEvent(102, hotel.Id, "Uluslararası Bodrum Süperyat & Yelken Kupası", "Sports", "⛵", "#38bdf8", "Bodrum Marina & Yalıkavak", 1.8, today.AddDays(4), 18500, 94, 91, "30 farklı ülkeden 120 mega yat ve binlerce VIP armatör/ziyaretçi katılımı."),
                new CityRadarEvent(103, hotel.Id, "Ege Gurme & Şarap Tadım Festivali", "Festival", "🍷", "#f59e0b", "Yalıkavak Marina Sahil", 4.5, today.AddDays(1), 8400, 92, 85, "Michelin yıldızlı 8 şef ve butik bağ üreticilerinin tadım gecesi."),
                new CityRadarEvent(104, hotel.Id, "Uluslararası Estetik & Sağlık Kongresi", "Congress", "🩺", "#10b981", "Bodrum Kültür & Kongre Merkezi", 6.1, today.AddDays(5), 3200, 88, 78, "Avrupa ve Orta Doğu'dan 1.400 hekim ve delege konaklama talebi."),
                new CityRadarEvent(105, hotel.Id, "Beach Club Sezon Açılışı & DJ Solomun Set", "Concert", "🎧", "#8b5cf6", "Cennet Koyu Sahil Kulübü", 2.4, today.AddDays(3), 4200, 99, 93, "Elektronik müzik severlerin yüksek bütçeli konaklama akını.")
            });
        }
        else if (city.Contains("istanbul"))
        {
            list.AddRange(new[]
            {
                new CityRadarEvent(201, hotel.Id, "UEFA Şampiyonlar Ligi / Süper Derbi", "Sports", "⚽", "#3b82f6", "Tüpraş Stadyumu (Beşiktaş)", 1.2, today.AddDays(2), 42500, 100, 98, "Otele yürüme mesafesinde dev maç. Avrupa'dan binlerce taraftar ve VIP delege."),
                new CityRadarEvent(202, hotel.Id, "Andrea Bocelli Boğaz Özel Senfoni Konseri", "Concert", "🎻", "#ec4899", "Zorlu PSM Ana Sahne", 3.8, today.AddDays(3), 8500, 99, 94, "Dünyaca ünlü tenörün seçkin konseri. Yüksek gelir grubu misafir ilgisi."),
                new CityRadarEvent(203, hotel.Id, "Global AI & Web3 Finans Zirvesi", "Congress", "💻", "#06b6d4", "İstanbul Lütfi Kırdar Kongre Sarayı", 2.6, today.AddDays(4), 14000, 92, 89, "70 ülkeden teknoloji CEO'ları ve yatırım fonları delegasyonu."),
                new CityRadarEvent(204, hotel.Id, "Bosphorus International Regatta", "Sports", "⛵", "#38bdf8", "İstanbul Boğazı Hattı", 0.5, today.AddDays(1), 25000, 90, 86, "Boğaz boyunca yat yarışı ve kıyı otellerinde seyir terası talebi.")
            });
        }
        else if (city.Contains("antalya"))
        {
            list.AddRange(new[]
            {
                new CityRadarEvent(301, hotel.Id, "Uluslararası Antalya Altın Portakal Film Festivali", "Festival", "🎬", "#f59e0b", "Cam Piramit Kongre & Fuar", 4.2, today.AddDays(3), 22000, 96, 92, "Sinema yıldızları, yönetmenler ve uluslararası basın mensupları konaklaması."),
                new CityRadarEvent(302, hotel.Id, "Antalya Open ATP Tenis Şampiyonası", "Sports", "🎾", "#10b981", "Kaya Palazzo Tenis Kulübü", 8.4, today.AddDays(5), 11000, 91, 84, "Dünyaca ünlü raketlerin karşılaşması ve spor turizmi akını."),
                new CityRadarEvent(303, hotel.Id, "Aspendos Senfoni & Opera Geceleri", "Concert", "🎭", "#ec4899", "Aspendos Antik Tiyatrosu", 12.0, today.AddDays(2), 7000, 98, 89, "2000 yıllık antik atmosferde Aida Operası.")
            });
        }
        else if (city.Contains("kapadokya") || city.Contains("nevşehir"))
        {
            list.AddRange(new[]
            {
                new CityRadarEvent(401, hotel.Id, "Uluslararası Kapadokya Sıcak Hava Balon Festivali", "Festival", "🎈", "#f97316", "Göreme Vadisi", 2.1, today.AddDays(2), 35000, 99, 97, "Gökyüzünde aynı anda 150 dev figürlü balon gösterisi ve küresel turist akını."),
                new CityRadarEvent(402, hotel.Id, "Cappadox Müzik & Çağdaş Sanat Festivali", "Festival", "🎸", "#8b5cf6", "Uçhisar Kalesi Etekleri", 1.5, today.AddDays(4), 9500, 95, 91, "Doğal akustik vadilerde gün doğumu ve gün batımı konserleri.")
            });
        }
        else
        {
            list.AddRange(new[]
            {
                new CityRadarEvent(501, hotel.Id, "Bölgesel Kültür & Sanat Festivali", "Festival", "🎭", "#8b5cf6", "Şehir Meydanı", 2.0, today.AddDays(2), 12000, 93, 87, "Yerel lezzetler ve sokak konserleri."),
                new CityRadarEvent(502, hotel.Id, "Uluslararası İş & Sanayi Zirvesi", "Congress", "🤝", "#3b82f6", "Kongre Sarayı", 3.5, today.AddDays(4), 6500, 90, 82, "Bölgesel sanayici ve yatırımcı delegasyonu.")
            });
        }

        // Add custom simulated events
        list.AddRange(_customSimulatedEvents.Where(e => e.HotelId == hotel.Id));

        return list.OrderBy(e => e.Date).ToList();
    }

    public WeatherTourismImpact GetWeatherImpact(Hotel hotel)
    {
        var city = hotel.City.ToLowerInvariant();
        if (city.Contains("bodrum") || city.Contains("antalya") || city.Contains("çeşme") || city.Contains("fethiye"))
        {
            return new WeatherTourismImpact(
                "Güneşli, Sıcak & Açık Gökyüzü",
                "☀️",
                31,
                26,
                8,
                97,
                "İdeal deniz ve açık hava iklimi. Son dakika hafta sonu kaçamağı ve plaj kulübü rezervasyonlarını tetikleyen maksimum talep çarpanı (+%14)."
            );
        }
        if (city.Contains("istanbul"))
        {
            return new WeatherTourismImpact(
                "Açık & Hafif Boğaz Esintili",
                "🌤️",
                25,
                21,
                6,
                92,
                "Boğaz turları ve tarihi yarımada gezileri için mükemmel hava. Şehir otellerinde hafta sonu doluluk oranını canlı tutuyor."
            );
        }
        if (city.Contains("kapadokya"))
        {
            return new WeatherTourismImpact(
                "Sakin Rüzgar & Kristal Berrak",
                "🌅",
                23,
                18,
                5,
                98,
                "Balon uçuşları için %100 kusursuz sivil havacılık meteorolojisi (yeşil bayrak). Balon turizmi rezervasyon patlaması."
            );
        }

        return new WeatherTourismImpact(
            "Mevsim Normalleri & Açık",
            "🌤️",
            24,
            20,
            6,
            88,
            "Konforlu gezi ve etkinlik koşulları."
        );
    }

    public YieldSurgeAnalysis CalculateSurge(
        Hotel hotel, 
        int simulatedFlightBoostPercent = 0, 
        int simulatedAttendanceBoost = 0,
        int simulatedTempBoost = 0)
    {
        var airport = GetAirportInflow(hotel);
        var events = GetCityEvents(hotel);
        var weather = GetWeatherImpact(hotel);

        // Base ADR (Hotel Stars based)
        decimal baseAdr = hotel.Stars switch
        {
            5 => 8500m,
            4 => 5200m,
            _ => 3400m
        };

        // 1. Events Impact Factor
        int totalAttendance = events.Sum(e => e.ExpectedAttendance) + simulatedAttendanceBoost;
        int maxEventImpact = events.Any() ? events.Max(e => e.DemandImpactScore) : 70;
        double eventMultiplier = 1.0 + (maxEventImpact / 100.0 * 0.28) + (totalAttendance > 30000 ? 0.12 : 0.05);

        // 2. Flight Inflow Factor
        double flightLoadFactor = airport.AverageLoadFactor + (simulatedFlightBoostPercent * 0.4);
        double flightMultiplier = 1.0 + ((flightLoadFactor - 85.0) / 100.0 * 0.35);
        if (flightMultiplier < 1.0) flightMultiplier = 1.0;

        // 3. Weather Factor
        int finalTemp = weather.TempC + simulatedTempBoost;
        double weatherMultiplier = weather.TourismDemandScore >= 95 ? 1.08 : 1.03;

        // Combined Multiplier
        double combinedMultiplier = (eventMultiplier * 0.50) + (flightMultiplier * 0.35) + (weatherMultiplier * 0.15);
        combinedMultiplier = Math.Round(Math.Clamp(combinedMultiplier, 1.05, 1.85), 2);

        decimal surgedAdr = Math.Round(baseAdr * (decimal)combinedMultiplier / 10m) * 10m;

        int demandIndex = (int)Math.Clamp(Math.Round((combinedMultiplier - 1.0) / 0.85 * 100), 25, 99);

        string demandLevel;
        string levelColor;
        if (demandIndex >= 85)
        {
            demandLevel = "🔥 Kritik Talep Patlaması (Mega Surge)";
            levelColor = "#ef4444";
        }
        else if (demandIndex >= 65)
        {
            demandLevel = "⚡ Güçlü Talep Dalgası (High Surge)";
            levelColor = "#f59e0b";
        }
        else
        {
            demandLevel = "🟢 Dengeli & İstikrarlı Talep (Standard)";
            levelColor = "#10b981";
        }

        int occupancyProjected = (int)Math.Clamp(75 + (demandIndex * 0.24), 75, 99);
        decimal revParBase = baseAdr * 0.72m;
        decimal revParSurged = surgedAdr * (occupancyProjected / 100.0m);
        decimal projectedRevenueLift = Math.Round((revParSurged - revParBase) * 40 * 3); // 40 odalı hafta sonu farkı

        var drivers = new List<string>();
        if (events.Any())
        {
            var topEv = events.OrderByDescending(e => e.DemandImpactScore).First();
            drivers.Add($"🎵 {topEv.Title} (%{topEv.TicketSoldPercent} Bilet Satışı)");
        }
        drivers.Add($"✈️ {airport.AirportCode} Uçuş Doluluğu %{airport.AverageLoadFactor:F1} ({airport.TotalInboundPassengers:N0} Yolcu)");
        drivers.Add($"☀️ {weather.Condition} ({finalTemp}°C)");

        if (simulatedAttendanceBoost > 0)
            drivers.Add($"🧪 +{simulatedAttendanceBoost:N0} Ekstra Simüle Katılımcı");

        return new YieldSurgeAnalysis(
            baseAdr,
            combinedMultiplier,
            surgedAdr,
            demandIndex,
            demandLevel,
            levelColor,
            occupancyProjected,
            revParBase,
            revParSurged,
            projectedRevenueLift,
            drivers
        );
    }

    public List<OtaSyncChannel> GetOtaChannels(decimal baseRate, decimal surgeRate)
    {
        return new List<OtaSyncChannel>
        {
            new("booking", "Booking.com", "🏨", "#003580", baseRate, surgeRate, 78, true, "Senkronize"),
            new("airbnb", "Airbnb", "🏠", "#FF5A5F", baseRate, surgeRate, 114, true, "Senkronize"),
            new("expedia", "Expedia Partner", "✈️", "#FFDA00", baseRate, surgeRate, 92, true, "Senkronize"),
            new("agoda", "Agoda Global", "🌏", "#00a187", baseRate, surgeRate, 85, true, "Senkronize"),
            new("etstur", "ETS Tur & Odamax", "🌴", "#e11d48", baseRate, surgeRate, 64, true, "Senkronize"),
            new("google", "Google Hotel Ads", "🔍", "#4285F4", baseRate, surgeRate, 130, true, "Senkronize"),
            new("otelz", "Otelz.com", "🇹🇷", "#ea580c", baseRate, surgeRate, 58, true, "Senkronize")
        };
    }

    public void AddCustomSimulatedEvent(Hotel hotel, string title, string category, int attendance, int impact)
    {
        var newEv = new CityRadarEvent(
            1000 + _customSimulatedEvents.Count + 1,
            hotel.Id,
            title,
            category,
            category switch
            {
                "Concert" => "🎵",
                "Sports" => "⚽",
                "Festival" => "🎉",
                "Congress" => "🤝",
                _ => "✨"
            },
            "#8b5cf6",
            $"{hotel.City} Etkinlik Alanı",
            1.5,
            DateTime.Today.AddDays(2),
            attendance,
            100,
            impact,
            "Kullanıcı tarafından canlı simülasyon amacıyla eklenen özel talep olayı.",
            true
        );
        _customSimulatedEvents.Add(newEv);
    }

    public void ClearCustomEvents()
    {
        _customSimulatedEvents.Clear();
    }
}
