using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Otel.Web.Services;

/// <summary>Basit TR/EN yerelleştirme. Kullanım: L["anahtar"]</summary>
public class I18n(ProtectedLocalStorage storage)
{
    public string Lang { get; private set; } = "tr";
    public event Action? Changed;
    bool loaded;

    public async Task InitAsync()
    {
        if (loaded) return;
        try { var r = await storage.GetAsync<string>("lang"); if (r.Success && r.Value is "tr" or "en") Lang = r.Value!; } catch { }
        loaded = true;
    }

    public async Task SetAsync(string lang)
    {
        Lang = lang;
        await storage.SetAsync("lang", lang);
        Changed?.Invoke();
    }

    public string this[string key] => Dict.TryGetValue(key, out var v) ? (Lang == "tr" ? v.tr : v.en) : key;

    public System.Globalization.CultureInfo Culture => new(Lang == "tr" ? "tr-TR" : "en-US");

    static readonly Dictionary<string, (string tr, string en)> Dict = new()
    {
        // Menü
        ["nav.dashboard"] = ("Kontrol Paneli", "Dashboard"),
        ["nav.frontdesk"] = ("Ön Büro", "Front Desk"),
        ["nav.reservations"] = ("Rezervasyonlar", "Reservations"),
        ["nav.newres"] = ("Yeni Rezervasyon", "New Reservation"),
        ["nav.calendar"] = ("Oda Takvimi", "Room Calendar"),
        ["nav.rooms"] = ("Odalar", "Rooms"),
        ["nav.guests"] = ("Misafirler", "Guests"),
        ["nav.nearby"] = ("Çevre & Aktiviteler", "Nearby & Activities"),
        ["nav.channels"] = ("Kanal Yöneticisi", "Channel Manager"),
        ["nav.rates"] = ("Fiyatlandırma", "Pricing"),
        ["nav.housekeeping"] = ("Kat Hizmetleri", "Housekeeping"),
        ["nav.maintenance"] = ("Bakım / Arıza", "Maintenance"),
        ["nav.accounting"] = ("Ön Muhasebe", "Accounting"),
        ["nav.invoices"] = ("Faturalar", "Invoices"),
        ["nav.stock"] = ("Stok", "Stock"),
        ["nav.messages"] = ("Misafir İlişkileri", "Guest Relations"),
        ["nav.kbs"] = ("Kimlik Bildirim (KBS)", "ID Notification (KBS)"),
        ["nav.reports"] = ("Raporlar", "Reports"),
        ["nav.staff"] = ("Personel", "Staff"),
        ["nav.jobs"] = ("Kariyer & Personel", "Jobs & Staffing"),
        ["nav.pricing"] = ("Oda Bazlı Fiyatlandırma", "Room Pricing"),
        ["nav.admin"] = ("Yönetim", "Administration"),
        ["nav.booking"] = ("Online Rezervasyon", "Online Booking"),
        ["nav.logout"] = ("Çıkış", "Sign out"),
        ["sec.operations"] = ("OPERASYON", "OPERATIONS"),
        ["sec.sales"] = ("SATIŞ", "SALES"),
        ["sec.back"] = ("ARKA BÜRO", "BACK OFFICE"),
        ["sec.system"] = ("SİSTEM", "SYSTEM"),

        // Ortak
        ["c.search"] = ("Ara...", "Search..."),
        ["c.save"] = ("Kaydet", "Save"),
        ["c.cancel"] = ("İptal", "Cancel"),
        ["c.close"] = ("Kapat", "Close"),
        ["c.add"] = ("Ekle", "Add"),
        ["c.delete"] = ("Sil", "Delete"),
        ["c.edit"] = ("Düzenle", "Edit"),
        ["c.status"] = ("Durum", "Status"),
        ["c.total"] = ("Toplam", "Total"),
        ["c.paid"] = ("Ödenen", "Paid"),
        ["c.balance"] = ("Bakiye", "Balance"),
        ["c.nights"] = ("Gece", "Nights"),
        ["c.guest"] = ("Misafir", "Guest"),
        ["c.room"] = ("Oda", "Room"),
        ["c.roomtype"] = ("Oda Tipi", "Room Type"),
        ["c.checkin"] = ("Giriş", "Check-in"),
        ["c.checkout"] = ("Çıkış", "Check-out"),
        ["c.adults"] = ("Yetişkin", "Adults"),
        ["c.children"] = ("Çocuk", "Children"),
        ["c.channel"] = ("Kanal", "Channel"),
        ["c.board"] = ("Pansiyon", "Board"),
        ["c.price"] = ("Fiyat", "Price"),
        ["c.date"] = ("Tarih", "Date"),
        ["c.code"] = ("Kod", "Code"),
        ["c.name"] = ("Ad", "Name"),
        ["c.phone"] = ("Telefon", "Phone"),
        ["c.notes"] = ("Notlar", "Notes"),
        ["c.all"] = ("Tümü", "All"),
        ["c.none"] = ("Kayıt bulunamadı", "No records found"),
        ["c.actions"] = ("İşlemler", "Actions"),
        ["c.yes"] = ("Evet", "Yes"),
        ["c.no"] = ("Hayır", "No"),
        ["c.search.btn"] = ("Müsaitlik Ara", "Check Availability"),

        // Giriş
        ["login.title"] = ("Hoş geldiniz", "Welcome back"),
        ["login.sub"] = ("Otel yönetim paneline giriş yapın", "Sign in to the hotel management panel"),
        ["login.user"] = ("Kullanıcı adı", "Username"),
        ["login.pass"] = ("Şifre", "Password"),
        ["login.btn"] = ("Giriş Yap", "Sign In"),
        ["login.error"] = ("Kullanıcı adı veya şifre hatalı.", "Invalid username or password."),
        ["login.demo"] = ("Demo hesaplar", "Demo accounts"),

        // Dashboard
        ["dash.title"] = ("Kontrol Paneli", "Dashboard"),
        ["dash.occupancy"] = ("Bugünkü Doluluk", "Today's Occupancy"),
        ["dash.arrivals"] = ("Bugün Girişler", "Arrivals Today"),
        ["dash.departures"] = ("Bugün Çıkışlar", "Departures Today"),
        ["dash.inhouse"] = ("Konaklayan", "In-House"),
        ["dash.adr"] = ("Ort. Oda Fiyatı (ADR)", "Avg. Daily Rate (ADR)"),
        ["dash.revpar"] = ("RevPAR", "RevPAR"),
        ["dash.revenue30"] = ("Son 30 Gün Oda Geliri", "Room Revenue (30d)"),
        ["dash.chart.occ"] = ("Doluluk Trendi (±30 gün)", "Occupancy Trend (±30 days)"),
        ["dash.chart.channels"] = ("Kanal Dağılımı", "Channel Mix"),
        ["dash.rooms.status"] = ("Oda Durumları", "Room Status"),
        ["dash.alerts"] = ("Uyarılar", "Alerts"),

        // Durumlar
        ["st.Tentative"] = ("Ön Rezervasyon", "Tentative"),
        ["st.Confirmed"] = ("Onaylı", "Confirmed"),
        ["st.CheckedIn"] = ("Konaklıyor", "Checked-in"),
        ["st.CheckedOut"] = ("Çıkış Yaptı", "Checked-out"),
        ["st.Cancelled"] = ("İptal", "Cancelled"),
        ["st.NoShow"] = ("Gelmedi", "No-show"),
        ["hk.Clean"] = ("Temiz", "Clean"),
        ["hk.Dirty"] = ("Kirli", "Dirty"),
        ["hk.Cleaning"] = ("Temizleniyor", "Cleaning"),
        ["hk.Inspected"] = ("Kontrol Edildi", "Inspected"),
        ["room.Available"] = ("Kullanılabilir", "Available"),
        ["room.OutOfOrder"] = ("Arızalı", "Out of order"),
        ["room.Blocked"] = ("Blokajlı", "Blocked"),
    };
}
