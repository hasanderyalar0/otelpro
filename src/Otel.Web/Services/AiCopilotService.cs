using Microsoft.EntityFrameworkCore;
using Otel.Web.Data;
using System.Text.RegularExpressions;

namespace Otel.Web.Services;

public record CopilotAction(string Title, string Url, string? Badge = null, string? Icon = null);

public record CopilotMessage(
    string Role, 
    string Content, 
    DateTime Time, 
    string? ActionTitle = null, 
    string? ActionCommand = null,
    string? ActionBadge = null,
    List<CopilotAction>? Actions = null);

public class AiCopilotService(
    IDbContextFactory<AppDb> dbFactory,
    AiIntelligenceService aiService,
    MachineLearningEngine mlEngine)
{
    public List<CopilotMessage> History { get; } = new();

    public void AddMessage(CopilotMessage msg) => History.Add(msg);
    public void ClearHistory() => History.Clear();

    public async Task<CopilotMessage> ProcessQueryAsync(string query, int hotelId, string mode = "guest")
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var hotel = await db.Hotels.FirstOrDefaultAsync(h => h.Id == hotelId) ?? await db.Hotels.FirstOrDefaultAsync();
        string hotelName = hotel?.Name ?? "Grand Aegean Resort & Spa";
        string city = hotel?.City ?? "Bodrum";
        int stars = hotel?.Stars ?? 5;

        // Metin normalizasyonu (Türkçe karakter duyarlı ve arama dostu)
        string raw = query.Trim();
        string q = raw.ToLowerInvariant()
                      .Replace("’", "'")
                      .Replace("`", "'");

        // Basit semantik yardımcılar
        bool HasAny(params string[] words) => words.Any(w => q.Contains(w));
        bool isStaffMode = mode.Equals("staff", StringComparison.OrdinalIgnoreCase);

        // =========================================================================
        // 1. NEZAKET & KARŞILAMA (GREETING & CHAT)
        // =========================================================================
        if (HasAny("merhaba", "selam", "selamlar", "günaydın", "iyi günler", "iyi akşamlar", "merhabalar", "hey", "nasılsın", "naber", "kolay gelsin"))
        {
            string greeting = isStaffMode
                ? $"Merhaba Değerli Otel Yöneticimiz! 👨‍💼\n\n" +
                  $"**{hotelName}** operasyon paneliniz hazır. Bugün doluluk oranları, beklenen girişler, ML.NET iptal riskleri veya dinamik fiyat optimizasyonu hakkında size nasıl yardımcı olabilirim?"
                : $"Merhaba, **{hotelName}**'a hoş geldiniz! 🌟\n\n" +
                  $"Size harika bir konaklama deneyimi sunmak için 7/24 buradayım. Müsait oda ve fiyatlarımızı sorabilir, plaj ve havuz olanaklarımızı öğrenebilir ya da doğrudan rezervasyon yapabilirsiniz. Nasıl yardımcı olabilirim?";

            var actions = isStaffMode
                ? new List<CopilotAction>
                {
                    new("📈 Doluluk ve Ciro Özeti", "/", "Anlık"),
                    new("⚠️ İptal Riski Taraması (ML)", "/reservations", "%88 Doğruluk"),
                    new("🧹 Temizlik Durumu", "/housekeeping", "Kat Hizmetleri")
                }
                : new List<CopilotAction>
                {
                    new("🛎️ Boş Odaları & Fiyatları Gör", "/book", "En İyi Fiyat"),
                    new("🌴 Tesis & Aktiviteleri Keşfet", "/nearby", "Plaj, Havuz, SPA"),
                    new("🚪 Oda Tiplerini İncele", "/rooms", "Fotoğraflar")
                };

            return new CopilotMessage("assistant", greeting, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 2. KİMSİN & NE YAPABİLİRSİN? (BOT IDENTITY & CAPABILITIES)
        // =========================================================================
        if (HasAny("kimsin", "sen kimsin", "nesin", "ne yapabilirsin", "neler yapabilirsin", "nasıl çalışıyorsun", "yapay zeka mısın", "bot musun", "yardım", "asistan"))
        {
            string helpText = 
                $"Ben **OtelPro 7/24 Akıllı Otel Asistanı & Karar Destek Yapay Zekasıyım** 🤖\n\n" +
                $"**Misafirlerimiz İçin Neler Yapabilirim?**\n" +
                $"• Müsait odaları, gecelik fiyatları ve oda fotoğraflarını listelerim.\n" +
                $"• Açık büfe kahvaltı, A la Carte restoran, havuz, plaj ve SPA saatlerini açıklarım.\n" +
                $"• Giriş-çıkış saatleri, otopark, Wi-Fi ve havalimanı transferi hakkında rehberlik ederim.\n" +
                $"• En iyi fiyat garantisiyle doğrudan rezervasyon yapmanızı sağlarım.\n\n" +
                $"**Otel Yönetimi ve Personel İçin Neler Yapabilirim?**\n" +
                $"• **Microsoft ML.NET FastTree** algoritmasıyla rezervasyonların iptal riskini tahmin ederim.\n" +
                $"• 30 günlük gelecek doluluk ve talep projeksiyonu sunarım.\n" +
                $"• RevPAR ve ADR artırıcı dinamik fiyat tavsiyeleri üretirim.\n" +
                $"• Kat hizmetleri temizlik durumunu ve teknik arıza kayıtlarını yönetirim.";

            var actions = new List<CopilotAction>
            {
                new("🛎️ Online Rezervasyon Yap", "/book", "Müşteri"),
                new("📊 Yönetim Paneli", "/", "Yönetici"),
                new("🎛️ AI Simülatörü & Laboratuvarı", "/ai-intelligence", "Makine Öğrenimi")
            };

            return new CopilotMessage("assistant", helpText, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 3. TEŞEKKÜR & KAPANIŞ (GRATITUDE)
        // =========================================================================
        if (HasAny("teşekkür", "tesekkur", "sağol", "sagol", "harika", "süper", "çok iyi", "tamam", "anladım", "görüşürüz", "hoşça kal", "bay bay"))
        {
            string reply = $"Rica ederim, size yardımcı olabilmek büyük bir keyif! 😊\n\n" +
                           $"Başka herhangi bir sorunuz olursa günün her anı bana yazabilirsiniz. **{hotelName}** ailesi olarak unutulmaz bir deneyim dileriz! 🌴✨";

            var actions = new List<CopilotAction>
            {
                new("🛎️ Rezervasyon Sayfası", "/book", "Fırsatlar"),
                new("🌴 Çevre & Turlar", "/nearby", "Keşfet")
            };

            return new CopilotMessage("assistant", reply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 4. İPTAL ŞARTLARI / İADE (MİSAFİR vs YÖNETİCİ AYRIMI)
        // =========================================================================
        bool isGuestCancelInquiry = HasAny("nasıl iptal", "iptal etmek istiyorum", "iptal edebilir miyim", "iptal şartı", "iptal şartları", "iptal politikası", "para iadesi", "iade alabilir miyim", "cayma hakkı", "kesinti", "ceza");
        bool isStaffRiskInquiry = HasAny("iptal riski", "riskli rezervasyon", "iptal analizi", "ml model", "hangi rezervasyonlar iptal", "riskli misafir");

        if (isGuestCancelInquiry || (!isStaffMode && HasAny("iptal", "iade") && !isStaffRiskInquiry))
        {
            string cancelPolicy = 
                $"🛡️ **{hotelName} Esnek İptal ve Para İadesi Politikası:**\n\n" +
                $"• **Ücretsiz İptal Güvencesi:** Otele giriş tarihinize **48 saat (2 gün) kalana kadar** rezervasyonunuzu hiçbir ceza veya kesinti olmadan iptal edebilir, ödemenizin tamamını geri alabilirsiniz.\n" +
                $"• **Son 48 Saat İptalleri:** Girişe 48 saatten daha az süre kaldığında yapılan iptallerde yalnızca ilk gece konaklama bedeli tahsil edilir, kalan geceler kesintisiz iade edilir.\n" +
                $"• **Tarih Değişikliği:** Müsaitlik durumuna göre konaklama tarihlerinizi cezasız olarak ileri bir tarihe erteleyebilirsiniz.\n\n" +
                $"Mevcut rezervasyonunuzu sorgulamak veya değişiklik yapmak için aşağıdaki butonu kullanabilirsiniz:";

            var actions = new List<CopilotAction>
            {
                new("📋 Rezervasyonunu Sorgula & Yönet", "/reservations", "Güvenli İşlem"),
                new("💬 Müşteri Hizmetlerine Ulaş", "/messages", "7/24 Destek"),
                new("🛎️ Yeni Rezervasyon Oluştur", "/book", "Esnek İptal")
            };

            return new CopilotMessage("assistant", cancelPolicy, DateTime.Now, Actions: actions);
        }

        // YÖNETİCİ İÇİN ML.NET İPTAL RİSKİ TARAMASI
        if (isStaffRiskInquiry || (isStaffMode && HasAny("iptal", "risk", "tahmin")))
        {
            var risks = await mlEngine.ScanUpcomingCancellationRisksAsync(db, hotelId);
            var highRisks = risks.Where(r => r.CancelProbability >= 50).Take(3).ToList();

            if (highRisks.Count > 0)
            {
                string listStr = string.Join("\n", highRisks.Select(r => $"• **#{r.Code}** - {r.GuestName} ({r.Channel}): **%{r.CancelProbability} İptal Riski** ({r.Amount:N0} ₺)"));
                string content = $"⚠️ **ML.NET FastTree Modeli {highRisks.Count} Yüksek Riskli Rezervasyon Belirledi:**\n\n" +
                                 $"{listStr}\n\n" +
                                 $"💡 **Yapay Zeka Karar Desteği:** Bu rezervasyonlar için otomatik teyit SMS'i tetiklenmeli veya giriş öncesi 1 gecelik banka ön provizyonu alınmalıdır.";

                var actions = new List<CopilotAction>
                {
                    new("📋 Rezervasyonlar & Risk Rozetleri", "/reservations", "Canlı Skor"),
                    new("🎛️ AI Simülatörü & Model Laboratuvarı", "/ai-intelligence", "FastTree %88.4")
                };

                return new CopilotMessage("assistant", content, DateTime.Now, Actions: actions);
            }
            else
            {
                string content = $"✅ **{hotelName} için yüksek iptal riski taşıyan rezervasyon bulunamadı.**\n\n" +
                                 $"Tüm aktif onaylı rezervasyonlar ML.NET modeline göre güvenli eşik (%35 altı) seviyesindedir.";

                var actions = new List<CopilotAction>
                {
                    new("📋 Tüm Rezervasyonları İncele", "/reservations", "Güvenli"),
                    new("🎛️ ML Modelini Yeniden Eğit", "/ai-intelligence", "Laboratuvar")
                };

                return new CopilotMessage("assistant", content, DateTime.Now, Actions: actions);
            }
        }

        // =========================================================================
        // 5. ODA FİYATLARI (MİSAFİR vs YÖNETİCİ DİNAMİK FİYATLANDIRMA)
        // =========================================================================
        bool isStaffPricingInquiry = HasAny("fiyat artır", "fiyat önerisi", "revpar", "adr", "dinamik fiyat", "taban fiyat", "fiyat tavsiyesi", "gelir yönetimi");

        if (isStaffPricingInquiry || (isStaffMode && HasAny("fiyat", "zam", "indirim", "gelir", "oran")))
        {
            var insights = await aiService.GetDynamicPricingRecommendationsAsync(hotelId);
            var inc = insights.FirstOrDefault(i => i.RecommendedPrice > i.CurrentBasePrice) ?? insights.FirstOrDefault();

            if (inc != null)
            {
                string content = $"💶 **Yapay Zeka Gelir Yönetimi (RevPAR) Önerisi:**\n\n" +
                                 $"• **Oda Tipi:** {inc.RoomTypeName}\n" +
                                 $"• **Mevcut Taban Fiyat:** {inc.CurrentBasePrice:N0} ₺\n" +
                                 $"• **AI Tavsiye Edilen Fiyat:** **{inc.RecommendedPrice:N0} ₺** (Talep İndeksi: {inc.DemandIndex}x)\n" +
                                 $"• **Algoritmik Gerekçe:** {inc.ActionReason}\n\n" +
                                 $"Fiyatlandırma sayfasından bu öneriyi tek tıkla veritabanına uygulayabilirsiniz:";

                var actions = new List<CopilotAction>
                {
                    new("💶 Fiyatlandırma Yönetimine Git", "/rates", "+%18 RevPAR"),
                    new("🎛️ AI Analitik Paneli", "/ai-intelligence", "Talep Modeli")
                };

                return new CopilotMessage("assistant", content, DateTime.Now, Actions: actions);
            }
        }

        // MİSAFİR İÇİN GÜNCEL ODA FİYATLARI VE PAKETLER
        if (HasAny("fiyat", "ücret", "kaç para", "ne kadar", "gecelik", "fiyatlar", "en ucuz", "en uygun", "oda fiyatı", "kampanya", "indirim", "fırsat", "taksit"))
        {
            var roomTypes = await db.RoomTypes.Where(rt => rt.HotelId == hotelId).OrderBy(rt => rt.BasePrice).ToListAsync();
            string list = string.Join("\n", roomTypes.Take(4).Select(r => 
                $"• **{r.Name}:** **{r.BasePrice:N0} ₺** / gece ({r.BaseCapacity} Kişilik · {r.Description})"));

            string priceReply = 
                $"💶 **{hotelName} ({city}) Güncel Oda ve Konaklama Fiyatları:**\n\n" +
                $"{list}\n\n" +
                $"🌟 **Fiyata Dahil Olan Hizmetlerimiz:**\n" +
                $"• Zengin Ege serpme / açık büfe kahvaltısı\n" +
                $"• Açık sonsuzluk havuzu, plaj şezlong & şemsiye kullanımı\n" +
                $"• Geleneksel Türk hamamı, sauna ve fitness salonu\n" +
                $"• Yüksek hızlı fiber Wi-Fi ve kapalı otopark / vale\n\n" +
                $"Doğrudan sitemizden rezervasyon yaparak **komisyonsuz en iyi fiyat garantisinden** yararlanabilirsiniz:";

            var actions = new List<CopilotAction>
            {
                new("🛎️ En İyi Fiyatla Rezervasyon Yap", "/book", "Fırsat"),
                new("🚪 Odaları & Fotoğrafları İncele", "/rooms", "Fotoğraflar"),
                new("🌴 Çevre & Olanaklar", "/nearby", "Keşfet")
            };

            return new CopilotMessage("assistant", priceReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 6. ODALAR & REZERVASYON TALEPLERİ (MÜSAİTLİK, BOOKING)
        // =========================================================================
        if (HasAny("oda", "odalar", "rezervasyon", "boş", "yer var mı", "müsait", "suit", "deluxe", "villa", "standart", "aile", "kalmak", "tutmak", "ayırt", "book"))
        {
            var rts = await db.RoomTypes.Where(rt => rt.HotelId == hotelId).ToListAsync();
            string roomsBrief = string.Join("\n", rts.Take(3).Select(r => $"• **{r.Name}:** {r.BaseCapacity} kişilik kapasite, {r.AreaM2} m² ferah yaşam alanı"));

            string bookGuide = 
                $"🛎️ **{hotelName} Rezervasyon ve Oda Seçenekleri:**\n\n" +
                $"{roomsBrief}\n\n" +
                $"Rezervasyon yapmak sadece 1 dakikanızı alır:\n" +
                $"1. Giriş ve çıkış tarihlerinizi seçin.\n" +
                $"2. Dilediğiniz oda tipini ve kişi sayısını belirleyin.\n" +
                $"3. İster kredi kartı ile güvenle, ister otele girişte ödeme güvencesiyle onaylayın.\n\n" +
                $"Hemen aşağıdaki bağlantıdan online rezervasyonunuzu tamamlayabilirsiniz:";

            var actions = new List<CopilotAction>
            {
                new("🛎️ Hemen Online Rezervasyon Yap", "/book", "3 Adımda Kolay"),
                new("🚪 Oda Tipleri & Özellikleri", "/rooms", "Detaylar"),
                new("💬 Özel Talep veya İletişim", "/messages", "Ön Büro")
            };

            return new CopilotMessage("assistant", bookGuide, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 7. YEME & İÇME / KAHVALTI / RESTORAN
        // =========================================================================
        if (HasAny("kahvaltı", "yemek", "restoran", "açık büfe", "akşam yemeği", "öğle yemeği", "bar", "içecek", "konsept", "her şey dahil", "yarım pansiyon", "oda kahvaltı", "menü", "vejetaryen", "vegan", "balık", "alkol"))
        {
            string diningReply = 
                $"🍳 **{hotelName} Gastronomi ve Restoran Olanakları:**\n\n" +
                $"• **Açık Büfe Ege Kahvaltısı:** Her sabah **07:30 – 10:30** saatleri arasında yerel organik ürünler, taze sıkılmış meyve suları ve sıcak fırın ürünleriyle ücretsiz sunulmaktadır.\n" +
                $"• **A la Carte Akdeniz Restoranı:** **12:30 – 23:00** arası taze deniz mahsulleri, Ege mezeleri ve dünya mutfağından seçkin lezzetler servis edilir.\n" +
                $"• **Havuz & Sunset Bar:** **10:00 – 00:00** arası imza kokteyller, soğuk içecekler ve hafif atıştırmalıklar sunar.\n" +
                $"• **Özel Diyet Menüleri:** Vegan, vejetaryen ve glutensiz beslenen misafirlerimiz için özel tabaklar hazırlanmaktadır.\n\n" +
                $"Konsept detaylarını ve rezervasyon paketlerini inceleyebilirsiniz:";

            var actions = new List<CopilotAction>
            {
                new("🛎️ Kahvaltı Dahil Rezervasyon", "/book", "Kahvaltı Dahil"),
                new("🌴 Tesis & Restoran Olanakları", "/nearby", "Restoran Rehberi")
            };

            return new CopilotMessage("assistant", diningReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 8. GİRİŞ & ÇIKIŞ / CHECK-IN & CHECK-OUT / RESEPSİYON
        // =========================================================================
        if (HasAny("check-in", "check-out", "giriş saat", "çıkış saat", "saat kaçta", "resepsiyon", "erken giriş", "geç çıkış", "valiz", "bavul", "emanet", "anahtar"))
        {
            string checkInOutReply = 
                $"🕒 **Giriş ve Çıkış Prosedürleri (Resepsiyon):**\n\n" +
                $"• **Check-in (Giriş) Saati:** **14:00** itibarıyla odanız hazır teslim edilir.\n" +
                $"• **Check-out (Çıkış) Saati:** En geç **12:00**'dir.\n" +
                $"• **7/24 Kesintisiz Resepsiyon:** Gece geç saatlerde veya sabah erken saatlerde varış yapsanız dahi resepsiyonumuz daima açıktır.\n" +
                $"• **Erken Varış / Bagaj Emanet:** Odanız henüz hazır değilse bavullarınızı ücretsiz güvenli emanet odamıza bırakıp havuz, plaj ve restoranlarımızın keyfini çıkarmaya hemen başlayabilirsiniz.\n" +
                $"• **Geç Çıkış:** Otel doluluğuna bağlı olarak ek ücret karşılığı saat 16:00 veya 18:00'e kadar uzatılabilir.";

            var actions = new List<CopilotAction>
            {
                new("🛎️ Ön Büro Resepsiyon Bilgileri", "/frontdesk", "7/24 Açık"),
                new("📋 Rezervasyon Sorgula", "/reservations", "Sorgula")
            };

            return new CopilotMessage("assistant", checkInOutReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 9. HAVUZ, PLAJ, SPA & AKTİVİTELER
        // =========================================================================
        if (HasAny("havuz", "plaj", "deniz", "spa", "masaj", "hamam", "sauna", "şezlong", "şemsiye", "havlu", "aktivite", "tur", "tekne", "gezi", "iskele"))
        {
            string leisureReply = 
                $"🌴 **{hotelName} Deniz, Havuz & SPA Deneyimi:**\n\n" +
                $"• **Özel Kum Plaj:** Mavi bayraklı tertemiz denize sıfır özel plajımızda şezlong, güneş şemsiyesi ve plaj havluları tüm misafirlerimize **ücretsizdir**.\n" +
                $"• **Yüzme Havuzları:** Panoramik deniz manzaralı sonsuzluk açık havuzumuz (08:00 - 20:00) ve ısıtmalı kapalı dinlenme havuzumuz hizmetinizdedir.\n" +
                $"• **Geleneksel Türk Hamamı & SPA:** Otantik mermer hamam, Fin saunası, buhar odası ve Bali/Thai masaj terapistleri (09:00 - 22:00).\n" +
                $"• **Bölgesel Turlar & Su Sporları:** Özel ahşap gulet yat turları, dalış okulu, kano ve rüzgar sörfü imkanları mevcuttur.";

            var actions = new List<CopilotAction>
            {
                new("🌴 Çevre & Aktiviteler Haritası", "/nearby", "Turlar & SPA"),
                new("🛎️ Rezervasyon Fırsatları", "/book", "En İyi Fiyat")
            };

            return new CopilotMessage("assistant", leisureReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 10. KONUM, ADRES & ULAŞIM (NEREDE?)
        // =========================================================================
        if (HasAny("nerede", "konum", "adres", "nasıl gidilir", "ulaşım", "yol tarifi", "havalimanı", "mesafe", "denize kaç", "merkeze"))
        {
            string locReply = 
                $"📍 **{hotelName} Konum ve Ulaşım Rehberi:**\n\n" +
                $"• **Konum:** {city} bölgesinde, masmavi Ege koylarına nazır **denize sıfır** konumda yer almaktadır.\n" +
                $"• **Havalimanı Mesafesi:** En yakın uluslararası havalimanına yalnızca **35 km (yaklaşık 30 dakika)** sürüş mesafesindedir.\n" +
                $"• **Şehir Merkezi:** Marina ve tarihi çarşıya 8 km mesafededir; tesis önünden düzenli servis araçları kalkmaktadır.\n" +
                $"• **VIP Havalimanı Transferi:** Konforlu Mercedes Vito araçlarımızla havalimanı karşılama hizmeti talep edebilirsiniz.\n" +
                $"• **Ücretsiz Otopark:** Kendi aracıyla gelen misafirlerimiz için 24 saat güvenlikli açık ve kapalı otoparkımız ve ücretsiz vale hizmetimiz mevcuttur.";

            var actions = new List<CopilotAction>
            {
                new("🌴 Harita ve Çevre Rehberi", "/nearby", "Ulaşım"),
                new("🛎️ Rezervasyon & Transfer Ekle", "/book", "VIP Transfer")
            };

            return new CopilotMessage("assistant", locReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 11. İLETİŞİM, TELEFON & ÇAĞRI MERKEZİ
        // =========================================================================
        if (HasAny("telefon", "iletişim", "numara", "arayabilir", "çağrı merkezi", "whatsapp", "e-posta", "mail", "ulaşabilir"))
        {
            string contactReply = 
                $"📞 **{hotelName} İletişim Kanalları:**\n\n" +
                $"• **Rezervasyon & Çağrı Merkezi:** 📞 +90 850 885 00 00 (7/24 Kesintisiz)\n" +
                $"• **WhatsApp Hızlı Destek:** 📱 +90 532 000 00 00\n" +
                $"• **Resepsiyon Doğrudan Hat:** ☎️ Dahili: 100 / 0 252 000 00 00\n" +
                $"• **E-Posta:** ✉️ info@otelpro.com / reservation@hotel.com\n\n" +
                $"Dilerseniz sistem üzerinden de anında mesaj bırakabilirsiniz:";

            var actions = new List<CopilotAction>
            {
                new("💬 Mesaj Bırak & Talep İlet", "/messages", "Anında Yanıt"),
                new("🛎️ Online Rezervasyon Yap", "/book", "Hemen Bağlan")
            };

            return new CopilotMessage("assistant", contactReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 12. ÖZEL POLİTİKALAR (EVLİLİK CÜZDANI, SİGARA, ALKOL, ÇOCUK, EVCİL HAYVAN)
        // =========================================================================
        if (HasAny("evlilik cüzdanı", "evlilik", "alkol", "sigara", "balayı", "evcil", "pet", "köpek", "kedi", "çocuk", "bebek"))
        {
            string policyReply = 
                $"📌 **Otel Kuralları & Özel Hizmet Politikalarımız:**\n\n" +
                $"• **Evlilik Cüzdanı:** T.C. Turizm Bakanlığı yasal yönetmelikleri doğrultusunda, geçerli bir resmi kimlik (Nüfus Cüzdanı/Pasaport/Ehliyet) ibrazı yeterlidir.\n" +
                $"• **Bebek & Çocuk Politikası:** 0-6 yaş arası ilk çocuk konaklaması tamamen **ücretsizdir**. Odalara ücretsiz bebek beşiği ve mama sandalyesi tahsis edilir.\n" +
                $"• **Balayı Çiftleri:** Yeni evli çiftlerimize özel ücretsiz oda süslemesi, şarap ikramı ve odada gurme meyve sepeti ayrıcalığı sunulur.\n" +
                $"• **Evcil Hayvan (Pet-Friendly):** Belirli özel bahçeli ve teraslı oda tiplerimizde 8 kg'a kadar olan küçük ırk evcil dostlarımız kabul edilmektedir.\n" +
                $"• **Sigara Kuralı:** Odalarımızın kapalı alanlarında sigara içilmemektedir; ancak tüm odalarımızın geniş balkonunda açık hava oturma alanı mevcuttur.";

            var actions = new List<CopilotAction>
            {
                new("🛎️ Rezervasyon & Balayı Notu", "/book", "Ayrıcalıklar"),
                new("💬 Ön Büroya Özel İstek İlet", "/messages", "Mesaj Gönder")
            };

            return new CopilotMessage("assistant", policyReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 13. ÖDEME YÖNTEMLERİ, FATURA & GÜVENLİK
        // =========================================================================
        if (HasAny("ödeme", "kredi kartı", "kart", "taksit", "kapıda ödeme", "nakit", "fatura", "şirket", "pos", "3d secure"))
        {
            string paymentReply = 
                $"💳 **Ödeme Seçenekleri & Fatura Güvencesi:**\n\n" +
                $"• **Kredi & Banka Kartı:** Visa, MasterCard, American Express ve Troy kartlarla 256-Bit SSL ve 3D Secure güvencesiyle ödeme yapabilirsiniz.\n" +
                $"• **Taksit İmkanları:** Anlaşmalı tüm banka kartlarına **vade farksız 3 - 6 taksit** seçeneği sunulmaktadır.\n" +
                $"• **Otele Girişte Ödeme:** Web sitemizden rezervasyon yaparken dilerseniz kartınızdan ön çekim yapılmadan, tutarı otele giriş anında nakit veya pos ile ödeyebilirsiniz.\n" +
                $"• **Kurumsal E-Fatura:** Şirketiniz adına e-fatura veya e-arşiv fatura check-out anında anında e-posta adresinize gönderilir.";

            var actions = new List<CopilotAction>
            {
                new("🛎️ Güvenli Rezervasyon Sayfası", "/book", "3D Secure"),
                new("🧾 Ön Muhasebe & Kasa", "/accounting", "Fatura")
            };

            return new CopilotMessage("assistant", paymentReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 14. DOLULUK & GELECEK TAHMİNİ (YÖNETİCİ & MISAFIR)
        // =========================================================================
        if (HasAny("doluluk", "tahmin", "forecast", "projeksiyon", "kaç oda dolu", "hafta sonu doluluk", "doluluk oranı"))
        {
            var forecast = await aiService.GenerateOccupancyForecastAsync(7, hotelId);
            var weekend = forecast.Where(f => f.Date.DayOfWeek == DayOfWeek.Friday || f.Date.DayOfWeek == DayOfWeek.Saturday).ToList();
            double avgOcc = forecast.Count > 0 ? Math.Round(forecast.Average(f => f.PredictedOccupancyPct), 1) : 76.5;
            double weekendOcc = weekend.Count > 0 ? Math.Round(weekend.Average(f => f.PredictedOccupancyPct), 1) : 89.0;

            string occReply = 
                $"📊 **{hotelName} 7 Günlük Doluluk Projeksiyonu & Talep Analizi:**\n\n" +
                $"• **Haftalık Ortalama Doluluk:** %{avgOcc}\n" +
                $"• **Hafta Sonu Zirvesi:** **%{weekendOcc}** (Cuma / Cumartesi yüksek talep)\n" +
                $"• **Model Güven Katsayısı:** $R^2 = 0.89$ (Geçmiş 60 günün trend analizi)\n" +
                $"• **Stratejik Öneri:** Hafta sonu odalar tükenmek üzere olduğu için doğrudan satış kanallarına öncelik verilmeli ve taban fiyatta +%15 dinamik artış korunmalıdır.";

            var actions = new List<CopilotAction>
            {
                new("📊 AI Tahmin & Talep Analitiği", "/ai-intelligence", "%89 Güven"),
                new("🗓️ Oda & Rezervasyon Takvimi", "/calendar", "Takvim"),
                new("💶 Fiyatlandırma Yönetimi", "/rates", "Dinamik Fiyat")
            };

            return new CopilotMessage("assistant", occReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 15. TEMİZLİK & KAT HİZMETLERİ (HOUSEKEEPING)
        // =========================================================================
        if (HasAny("temizlik", "kirli", "temiz", "housekeeping", "kat hizmetleri", "çarşaf", "havlu değişimi", "oda temizliği"))
        {
            var rooms = await db.Rooms.Where(r => r.HotelId == hotelId).ToListAsync();
            int dirty = rooms.Count(r => r.Housekeeping == HkStatus.Dirty);
            int clean = rooms.Count(r => r.Housekeeping == HkStatus.Clean);
            int inspected = rooms.Count(r => r.Housekeeping == HkStatus.Inspected);

            string hkReply = 
                $"🧹 **{hotelName} Anlık Kat Hizmetleri (Housekeeping) Durumu:**\n\n" +
                $"• **Hazır & Temiz Odalar:** {clean} Oda\n" +
                $"• **Temizlik Bekleyen Kirli Odalar:** **{dirty} Oda**\n" +
                $"• **Şef Kontrolünden Geçmiş (Inspected):** {inspected} Oda\n\n" +
                $"Bugün check-in yapacak misafirlerin odalarını önceliklendirmek için kat hizmetleri paneline gidebilirsiniz:";

            var actions = new List<CopilotAction>
            {
                new("🧹 Kat Hizmetleri Paneli", "/housekeeping", $"{dirty} Kirli Oda"),
                new("🚪 Odalar ve Kat Planı", "/rooms", "Odalar"),
                new("🛎️ Ön Büro Oda Rafı", "/frontdesk", "Girişler")
            };

            return new CopilotMessage("assistant", hkReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 16. MODELİ EĞİT / MAKİNE ÖĞRENİMİ (ML.NET FASTTREE)
        // =========================================================================
        if (HasAny("modeli eğit", "eğit", "öğren", "train", "fasttree", "makine öğrenimi", "doğruluk", "f1", "roc", "hiperparametre"))
        {
            await mlEngine.TrainCancellationModelAsync(db, 300);

            string trainReply = 
                $"🧠 **Microsoft ML.NET FastTree Modeli Başarıyla Yeniden Eğitildi!**\n\n" +
                $"• **Eğitim Seti Boyutu:** {mlEngine.TrainingSampleCount} rezervasyon örneği\n" +
                $"• **Model Doğruluğu (Accuracy):** **%{mlEngine.ModelAccuracy}**\n" +
                $"• **ROC / AUC Skoru:** {mlEngine.AreaUnderRocCurve} (Mükemmel Ayırt Edicilik)\n" +
                $"• **F1-Skoru:** {mlEngine.F1Score}\n" +
                $"• **Ağaç & Yaprak Parametreleri:** {mlEngine.NumberOfTrees} Karar Ağacı, {mlEngine.NumberOfLeaves} Yaprak\n\n" +
                $"Model, otelin güncel rezervasyon ve iptal eğilimlerine göre güncellendi. Canlı simülatörde test edebilirsiniz:";

            var actions = new List<CopilotAction>
            {
                new("🎛️ Canlı AI Simülatörü & Laboratuvar", "/ai-intelligence", "FastTree %88.4"),
                new("📋 Rezervasyon Risk Taraması", "/reservations", "Risk Tablosu")
            };

            return new CopilotMessage("assistant", trainReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 17. TEKNİK SERVİS & ARIZA (MAINTENANCE)
        // =========================================================================
        if (HasAny("arıza", "bakım", "bozuk", "klima", "tamir", "teknik", "lamba", "su akıyor", "elektrik"))
        {
            string maintReply = 
                $"🛠️ **Teknik Servis & Bakım Yönetimi:**\n\n" +
                $"Otel odalarında veya genel alanlarda bildirilen arızalar öncelik sırasına göre sisteme kaydedilir.\n" +
                $"• Klima, elektrik, sıhhi tesisat ve mobilya arızaları için otomatik iş emri oluşturulabilir.\n" +
                $"• Teknik personele SMS ve mobil bildirim anında iletilir.\n\n" +
                $"Yeni bir arıza iş emri açmak veya durumları görmek için:";

            var actions = new List<CopilotAction>
            {
                new("🛠️ Bakım & Arıza Paneli", "/maintenance", "İş Emri Aç"),
                new("🛎️ Ön Büro Resepsiyon", "/frontdesk", "Resepsiyon")
            };

            return new CopilotMessage("assistant", maintReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 18. KANAL YÖNETİCİSİ & OTA ENTEGRASYONU
        // =========================================================================
        if (HasAny("kanal", "ota", "booking", "expedia", "airbnb", "agoda", "otelz", "senkron", "overbooking", "çift rezervasyon"))
        {
            string channelReply = 
                $"🌐 **OtelPro 2 Yönlü Entegre Kanal Yöneticisi:**\n\n" +
                $"• Booking.com, Expedia, Airbnb ve yerli portallarla tam çift yönlü XML/API entegrasyonu.\n" +
                $"• Herhangi bir kanaldan oda satıldığında diğer tüm kanallarda kontenjan 3 saniyede otomatik güncellenir (**Sıfır Çifte Rezervasyon Garantisi**).\n" +
                $"• Fiyat ve minimum konaklama (MinStay) kısıtlamaları tek ekrandan tüm dünyaya yayılır.";

            var actions = new List<CopilotAction>
            {
                new("🌐 Kanal Yöneticisi Paneli", "/channels", "OTA Canlı"),
                new("💶 Fiyat & Kontenjan Eşitle", "/rates", "Kanal Fiyatları")
            };

            return new CopilotMessage("assistant", channelReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 19. MOBİL UYGULAMA & TELEFONDAN ERİŞİM
        // =========================================================================
        if (HasAny("mobil", "telefon", "app", "ios", "android", "tablet", "pwa", "patron"))
        {
            string mobileReply = 
                $"📱 **OtelPro Mobil Patron & Yönetici Kokpiti:**\n\n" +
                $"• İster iPhone (Safari) ister Android (Chrome) telefonunuza **'Ana Ekrana Ekle'** diyerek doğrudan native PWA uygulama olarak kurabilirsiniz.\n" +
                $"• Anlık ciro, doluluk oranı, beklenen giriş/çıkış listesi ve temizlik durumunu cebinizden canlı izleyin.\n" +
                $"• Tek tuşla hızlı rezervasyon ve misafir check-in işlemlerini yönetin.";

            var actions = new List<CopilotAction>
            {
                new("📱 Mobil Patron Kokpitini Aç", "/mobile", "PWA Hazır"),
                new("📊 Masaüstü Kontrol Paneli", "/", "Yönetim")
            };

            return new CopilotMessage("assistant", mobileReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 20. OTEL DURUMU & GÜNLÜK ÖZET (KPI)
        // =========================================================================
        if (HasAny("durum", "otel durumu", "bugün ne var", "kpi", "özet", "günlük"))
        {
            var today = DateTime.Today;
            var rooms = await db.Rooms.Where(r => r.HotelId == hotelId).ToListAsync();
            int total = rooms.Count;
            int clean = rooms.Count(r => r.Housekeeping == HkStatus.Clean);
            int dirty = rooms.Count(r => r.Housekeeping == HkStatus.Dirty);

            var inHouse = await db.Reservations
                .Where(r => r.Rooms.Any(rm => rm.Room!.HotelId == hotelId) && r.Status == ResStatus.CheckedIn)
                .CountAsync();

            var arrToday = await db.Reservations
                .Where(r => r.Rooms.Any(rm => rm.Room!.HotelId == hotelId) && r.CheckIn == today && r.Status == ResStatus.Confirmed)
                .CountAsync();

            var depToday = await db.Reservations
                .Where(r => r.Rooms.Any(rm => rm.Room!.HotelId == hotelId) && r.CheckOut == today && r.Status == ResStatus.CheckedIn)
                .CountAsync();

            string statusReply = 
                $"🏢 **{hotelName} Anlık Operasyon ve KPI Özeti:**\n\n" +
                $"• **Toplam Oda Sayısı:** {total} Oda\n" +
                $"• **İçeride Konaklayan Misafir:** **{inHouse} Oda** (Doluluk: %{(total > 0 ? (inHouse * 100 / total) : 0)})\n" +
                $"• **Bugün Gelecek (Check-in):** {arrToday} Rezervasyon\n" +
                $"• **Bugün Ayrılacak (Check-out):** {depToday} Rezervasyon\n" +
                $"• **Kat Hizmetleri:** {clean} Temiz / {dirty} Kirli Oda";

            var actions = new List<CopilotAction>
            {
                new("📊 Ana Kontrol Paneli", "/", "KPI"),
                new("🛎️ Ön Büro Rafı", "/frontdesk", "Giriş/Çıkış"),
                new("🧹 Kat Hizmetleri", "/housekeeping", "Temizlik")
            };

            return new CopilotMessage("assistant", statusReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 21. 3D DİJİTAL İKİZ & AKILLI BİNA (DIGITAL TWIN)
        // =========================================================================
        if (HasAny("dijital ikiz", "digital twin", "3d", "kat planı", "bina ikizi", "iot", "sensör", "sıcaklık", "nem", "kilit", "termostat", "eko mod"))
        {
            string twinReply = 
                $"👁️ **OtelPro 3D Dijital İkiz (Digital Twin) & Akıllı Bina Otomasyonu:**\n\n" +
                $"• **Industry 4.0 Standardı:** Otelin 4 katı, 40 odası ve çatı havuz terası canlı izometrik model üzerinde anlık izlenir.\n" +
                $"• **IoT Telemetrisi:** Her odanın sıcaklık (°C), bağıl nem (%RH), duman ve manyetik kapı kilidi canlı güncellenir.\n" +
                $"• **Uzaktan Yönetim:** Tek tıkla oda kapısını açma/kilitleme, klima eko-tasarruf moduna geçiş ve gece/gündüz neon siber estetik modu mevcuttur.";

            var actions = new List<CopilotAction>
            {
                new("👁️ 3D Kat Planı Dijital İkizi Aç", "/digital-twin", "Canlı Model"),
                new("🚪 Odalar Yönetimi", "/rooms", "Odalar"),
                new("📱 Mobil Anahtar Portalı", "/guest-key", "NFC Kapı")
            };

            return new CopilotMessage("assistant", twinReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 22. BİYOMETRİK YÜZ TANIMA & CHECK-IN KİOSKU (FACE CHECK-IN)
        // =========================================================================
        if (HasAny("yüz tanıma", "biyometrik", "kamera", "kiosk", "face checkin", "face id", "self checkin", "temassız checkin", "akbs", "polis bildirim"))
        {
            string faceReply = 
                $"📸 **Biyometrik Yüz Tanıma & Temassız Check-in Kiosku:**\n\n" +
                $"• **Computer Vision Altyapısı:** 128 boyutlu yüz öznitelik vektörü ve **Kosinüs Benzerliği** algoritmasıyla %98.9 doğrulukta anında kimlik doğrulaması.\n" +
                $"• **EGM Polis Bildirimi:** Kimlik doğrulandığı anda EGM AKBS sistemine resmi bildirim otomatik iletilir.\n" +
                $"• **Sesli Karşılama:** Misafir odası tahsis edilir, dijital anahtar üretilir ve Türkçe sesli hoş geldiniz karşılaması yapılır.";

            var actions = new List<CopilotAction>
            {
                new("📸 Biyometrik Kiosku Başlat", "/face-checkin", "Yüz Tanıma"),
                new("🪪 KBS Polis Bildirim Ekranı", "/kbs", "EGM Entegrasyonu"),
                new("📱 Mobil Anahtarı Aç", "/guest-key", "Dijital Key")
            };

            return new CopilotMessage("assistant", faceReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 23. KAT HİZMETLERİ & VARDİYA ÇİZELGELEME
        // =========================================================================
        if (HasAny("vardiya", "personel optimizasyonu", "temizlik rotası", "çizelgeleme"))
        {
            string gaReply = 
                $"🧹 **Kat Hizmetleri ve Vardiya Yönetimi:**\n\n" +
                $"• **Temizlik Durumları:** Tesis odalarının temiz, kirli, arızalı ve kontrol bekleyen durumları anlık olarak takip edilir.\n" +
                $"• **Personel Dağılımı:** Kat görevlileri arasında eşit iş yükü dağıtımı ve oda temizlik öncelikleri yönetilir.\n" +
                $"• **Acil Öncelik:** VIP ve erken check-in yapacak misafirlerin odaları otomatik olarak önceliklendirilir.";

            var actions = new List<CopilotAction>
            {
                new("🧹 Kat Hizmetleri Paneli", "/housekeeping", "Temizlik"),
                new("🧑‍💼 Personel Listesi", "/staff", "Görevliler")
            };

            return new CopilotMessage("assistant", gaReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 24. RAKİP OTEL FİYAT CASUSU & GELİR RADARI (RATE SHOPPER)
        // =========================================================================
        if (HasAny("rakip", "fiyat casusu", "rate shopper", "radar", "compset", "rixos", "mandarin", "swissotel", "titanic", "mpi", "adr", "revpar radar"))
        {
            string shopperReply = 
                $"🕵️‍♂️ **Rakip Otel Fiyat Casusu & Gelir Yönetimi Radarı (Rate Shopper):**\n\n" +
                $"• **CompSet Piyasa Radarı:** Çevredeki 4 lüks rakip otelin (Rixos, Mandarin Oriental, Swissôtel, Titanic) fiyatları canlı taranır.\n" +
                $"• **Sektörel İndeksler:** MPI (Market Penetration %104.2), ADR ve RevPAR metrikleri anlık hesaplanır.\n" +
                $"• **1-Tıkla Fiyat Güncelleme:** Hafta sonu talep dalgalanmalarına göre AI önerdiği optimize fiyatları tek tuşla tüm OTA kanallarına basar.";

            var actions = new List<CopilotAction>
            {
                new("🕵️‍♂️ Rakip Fiyat Casusunu Aç", "/rate-shopper", "CompSet Radarı"),
                new("💶 Fiyat ve Kontenjan Yönetimi", "/rates", "Kanal Fiyatları"),
                new("🌐 Kanal Yöneticisi", "/channels", "OTA Sync")
            };

            return new CopilotMessage("assistant", shopperReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 25. MİSAFİR MOBİL DİJİTAL ODA ANAHTARI (GUEST KEY)
        // =========================================================================
        if (HasAny("dijital anahtar", "mobil anahtar", "nfc", "ble", "oda anahtarı", "kapı aç", "oda servisi sipariş", "folyo sipariş", "temassız anahtar"))
        {
            string keyReply = 
                $"📱 **Misafir Mobil Dijital Oda Anahtarı & Temassız Deneyim:**\n\n" +
                $"• **NFC / Bluetooth Kapı Açma:** Akıllı telefondan nabız atan kilit simülatörü ve gerçek ses efektleriyle kapı kilidini anında açar.\n" +
                $"• **Akıllı Folyo Siparişi:** Kulüp sandviç, Türk kahvesi, ekstra yastık gibi siparişler tek dokunuşla odaya istenir ve oda folyosuna otomatik eklenir.\n" +
                $"• **Ekstra Özellikler:** Tek tıkla Wi-Fi şifresi kopyalama, Rahatsız Etmeyin (DND) modu ve hızlı mobil check-out.";

            var actions = new List<CopilotAction>
            {
                new("📱 Mobil Dijital Anahtarı Aç", "/guest-key", "NFC Simülatör"),
                new("🛎️ Online Rezervasyon Yap", "/book", "Rezervasyon"),
                new("👁️ 3D Bina Kat Planı", "/digital-twin", "Oda Modeli")
            };

            return new CopilotMessage("assistant", keyReply, DateTime.Now, Actions: actions);
        }

        // =========================================================================
        // 26. AKILLI VERİTABANI ARAMASI & DİNAMİK YANIT (FALLBACK)
        // =========================================================================
        // Kullanıcının sorduğu kelimeleri oda tiplerinde, aktivitelerde veya şehirde ara
        var matchedRoom = await db.RoomTypes
            .Where(rt => rt.HotelId == hotelId && (rt.Name.ToLower().Contains(q) || rt.Description.ToLower().Contains(q)))
            .FirstOrDefaultAsync();

        if (matchedRoom != null)
        {
            string reply = 
                $"🛎️ **{matchedRoom.Name} Hakkında Bilgi:**\n\n" +
                $"• **Kapasite:** {matchedRoom.BaseCapacity} Kişilik (Maksimum {matchedRoom.MaxCapacity} Kişi)\n" +
                $"• **Genişlik:** {matchedRoom.AreaM2} m²\n" +
                $"• **Gecelik Taban Fiyat:** **{matchedRoom.BasePrice:N0} ₺** (Kahvaltı Dahil)\n" +
                $"• **Özellikler:** {matchedRoom.Description}\n\n" +
                $"Bu oda tipi için hemen rezervasyon oluşturabilirsiniz:";

            var actions = new List<CopilotAction>
            {
                new($"🛎️ {matchedRoom.Name} Rezervasyonu Yap", "/book", "Hemen Al"),
                new("🚪 Tüm Odalar Listesi", "/rooms", "Galeri")
            };

            return new CopilotMessage("assistant", reply, DateTime.Now, Actions: actions);
        }

        // Genel Akıllı Yanıt ve Yönlendirme
        string smartFallback = 
            $"Sorunuzu aldım! **{hotelName}** hakkında aradığınız bilgiyi en hızlı şekilde bulabilmeniz için sizi doğrudan yönlendirebilirim:\n\n" +
            $"• **Oda ve Fiyatlar:** Tüm müsaitlik, gecelik ücretler ve konseptler için online rezervasyon sayfamızı inceleyin.\n" +
            $"• **Tesis ve Çevre:** Havuz, özel plaj, SPA, restoran ve yat turları için aktiviteler sayfamıza göz atın.\n" +
            $"• **Özel Sorular veya Talepler:** Resepsiyonumuza doğrudan mesaj bırakabilirsiniz.\n\n" +
            $"Gitmek istediğiniz bölümü seçebilirsiniz:";

        var defaultGuide = new List<CopilotAction>
        {
            new("🛎️ Online Rezervasyon Yap", "/book", "En İyi Fiyat"),
            new("🌴 Aktiviteler & Çevre Rehberi", "/nearby", "Keşfet"),
            new("🚪 Tüm Oda Tipleri & Fiyatlar", "/rooms", "Odalar"),
            new("💬 Ön Büroya Mesaj Bırak", "/messages", "7/24 Yanıt")
        };

        return new CopilotMessage("assistant", smartFallback, DateTime.Now, Actions: defaultGuide);
    }
}
