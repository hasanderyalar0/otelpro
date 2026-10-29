using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Otel.Web.Data;

// ───────── Enumlar (veritabanında string olarak saklanır) ─────────
public enum RoomStatus { Available, OutOfOrder, Blocked }
public enum HkStatus { Clean, Dirty, Cleaning, Inspected }
public enum ResStatus { Tentative, Confirmed, CheckedIn, CheckedOut, Cancelled, NoShow }
public enum AppRole { Admin, Manager, FrontDesk, Housekeeping, Accounting, Sales }
public enum ChargeCategory { Room, Restaurant, Spa, Minibar, Laundry, Transfer, Other }
public enum AccountType { Customer, Supplier, Agency }
public enum TaskType { DailyClean, Turnover, DeepClean, Inspection }
public enum WorkStatus { Open, InProgress, Done }
public enum Priority { Low, Normal, High, Urgent }
public enum MsgType { Sms, Email }
public enum MsgTrigger { ReservationCreated, PreArrival, CheckIn, CheckOut, PostStay, Manual }
public enum StockMoveType { In, Out, Adjust }

// ───────── Tesis ─────────
public class Hotel
{
    public int Id { get; set; }
    [MaxLength(150)] public string Name { get; set; } = "";
    [MaxLength(300)] public string Address { get; set; } = "";
    [MaxLength(80)] public string City { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    [MaxLength(120)] public string Email { get; set; } = "";
    [MaxLength(20)] public string TaxId { get; set; } = "";
    public int Stars { get; set; } = 4;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    [MaxLength(500)] public string? ImageUrl { get; set; }
    public TimeSpan CheckInTime { get; set; } = new(14, 0, 0);
    public TimeSpan CheckOutTime { get; set; } = new(12, 0, 0);
    public List<Floor> Floors { get; set; } = new();
    public List<RoomType> RoomTypes { get; set; } = new();
}

public class Floor
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public Hotel? Hotel { get; set; }
    public int Number { get; set; }
    [MaxLength(60)] public string Name { get; set; } = "";
}

public class RoomType
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public Hotel? Hotel { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(80)] public string NameEn { get; set; } = "";
    [MaxLength(500)] public string Description { get; set; } = "";
    public int BaseCapacity { get; set; } = 2;
    public int MaxCapacity { get; set; } = 3;
    [Column(TypeName = "decimal(10,2)")] public decimal BasePrice { get; set; }
    public int AreaM2 { get; set; }
    [MaxLength(40)] public string BedType { get; set; } = "";
    public List<Room> Rooms { get; set; } = new();
    public List<RoomTypeFeature> Features { get; set; } = new();
}

public class RoomFeature
{
    public int Id { get; set; }
    [MaxLength(60)] public string Name { get; set; } = "";
    [MaxLength(60)] public string NameEn { get; set; } = "";
    [MaxLength(10)] public string Icon { get; set; } = "✔";
}

public class RoomTypeFeature
{
    public int RoomTypeId { get; set; }
    public RoomType? RoomType { get; set; }
    public int FeatureId { get; set; }
    public RoomFeature? Feature { get; set; }
}

public class Room
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public int FloorId { get; set; }
    public Floor? Floor { get; set; }
    public int RoomTypeId { get; set; }
    public RoomType? RoomType { get; set; }
    [MaxLength(10)] public string Number { get; set; } = "";
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public HkStatus Housekeeping { get; set; } = HkStatus.Clean;
}

// ───────── Misafir ─────────
public class Guest
{
    public int Id { get; set; }
    [MaxLength(80)] public string FirstName { get; set; } = "";
    [MaxLength(80)] public string LastName { get; set; } = "";
    [MaxLength(120)] public string? Email { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(60)] public string Nationality { get; set; } = "TR";
    public DateTime? BirthDate { get; set; }
    public bool IsVip { get; set; }
    public int LoyaltyPoints { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<GuestDocument> Documents { get; set; } = new();
    [NotMapped] public string FullName => $"{FirstName} {LastName}";
}

public class GuestDocument
{
    public int Id { get; set; }
    public int GuestId { get; set; }
    public Guest? Guest { get; set; }
    [MaxLength(20)] public string DocType { get; set; } = "TC"; // TC / Passport
    [MaxLength(30)] public string DocNumber { get; set; } = "";
    [MaxLength(60)] public string? IssueCountry { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class BlacklistEntry
{
    public int Id { get; set; }
    public int GuestId { get; set; }
    public Guest? Guest { get; set; }
    [MaxLength(300)] public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int? CreatedByUserId { get; set; }
}

public class GuestNote
{
    public int Id { get; set; }
    public int GuestId { get; set; }
    public Guest? Guest { get; set; }
    [MaxLength(500)] public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

// ───────── Satış / Kanal ─────────
public class Channel
{
    public int Id { get; set; }
    [MaxLength(60)] public string Name { get; set; } = "";
    [MaxLength(20)] public string Code { get; set; } = "";
    [Column(TypeName = "decimal(5,2)")] public decimal CommissionRate { get; set; }
    public bool IsConnected { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSync { get; set; }
}

public class Agency
{
    public int Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(80)] public string? ContactName { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(120)] public string? Email { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal CommissionRate { get; set; }
}

public class BoardType
{
    public int Id { get; set; }
    [MaxLength(5)] public string Code { get; set; } = "";
    [MaxLength(60)] public string Name { get; set; } = "";
    [MaxLength(60)] public string NameEn { get; set; } = "";
    [Column(TypeName = "decimal(10,2)")] public decimal PerPersonPerNight { get; set; }
}

public class RatePlan
{
    public int Id { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    public int BoardTypeId { get; set; }
    public BoardType? BoardType { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal PriceFactor { get; set; } = 1m;
    public bool IsRefundable { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class SeasonalRate
{
    public int Id { get; set; }
    public int RoomTypeId { get; set; }
    public RoomType? RoomType { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal PricePerNight { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal ExtraAdultPrice { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal ChildPrice { get; set; }
    public int MinStay { get; set; } = 1;
}

public class Promotion
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(100)] public string Name { get; set; } = "";
    [Column(TypeName = "decimal(5,2)")] public decimal DiscountPercent { get; set; }
    public int MinNights { get; set; } = 1;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Currency
{
    [Key, MaxLength(3)] public string Code { get; set; } = "TRY";
    [MaxLength(50)] public string Name { get; set; } = "";
    [MaxLength(5)] public string Symbol { get; set; } = "₺";
    [Column(TypeName = "decimal(12,4)")] public decimal RateToTry { get; set; } = 1m;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>Kanal yöneticisi havuz müsaitlik: tarih × oda tipi × kanal.</summary>
public class ChannelInventory
{
    public int Id { get; set; }
    public int ChannelId { get; set; }
    public Channel? Channel { get; set; }
    public int RoomTypeId { get; set; }
    public RoomType? RoomType { get; set; }
    public DateTime Date { get; set; }
    public int Allotment { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal Price { get; set; }
    public int MinStay { get; set; } = 1;
    public bool StopSell { get; set; }
    public bool IsSynced { get; set; } = true;
}

// ───────── Rezervasyon ─────────
public class Reservation
{
    public int Id { get; set; }
    [MaxLength(20)] public string Code { get; set; } = "";
    public int GuestId { get; set; }
    public Guest? Guest { get; set; }
    public int? ChannelId { get; set; }
    public Channel? Channel { get; set; }
    public int? AgencyId { get; set; }
    public Agency? Agency { get; set; }
    public int? PromotionId { get; set; }
    public Promotion? Promotion { get; set; }
    public int? RatePlanId { get; set; }
    public RatePlan? RatePlan { get; set; }
    public int BoardTypeId { get; set; }
    public BoardType? BoardType { get; set; }
    public ResStatus Status { get; set; } = ResStatus.Confirmed;
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Adults { get; set; } = 2;
    public int Children { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal TotalAmount { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal DiscountAmount { get; set; }
    [MaxLength(3)] public string CurrencyCode { get; set; } = "TRY";
    [MaxLength(500)] public string? Notes { get; set; }
    public bool IsGroup { get; set; }
    [MaxLength(100)] public string? GroupName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int? CreatedByUserId { get; set; }
    public List<ReservationRoom> Rooms { get; set; } = new();
    public List<ReservationGuest> Guests { get; set; } = new();
    public Folio? Folio { get; set; }
    [NotMapped] public int Nights => Math.Max(1, (CheckOut.Date - CheckIn.Date).Days);
}

public class ReservationRoom
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    public int RoomTypeId { get; set; }
    public RoomType? RoomType { get; set; }
    public int? RoomId { get; set; }
    public Room? Room { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal NightlyRate { get; set; }
    public int Adults { get; set; } = 2;
    public int Children { get; set; }
}

public class ReservationGuest
{
    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    public int GuestId { get; set; }
    public Guest? Guest { get; set; }
    public bool IsPrimary { get; set; }
}

// ───────── Finans ─────────
public class Account
{
    public int Id { get; set; }
    [MaxLength(150)] public string Name { get; set; } = "";
    public AccountType Type { get; set; } = AccountType.Customer;
    [MaxLength(20)] public string? TaxNo { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Balance { get; set; }
}

public class Folio
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    public int? AccountId { get; set; }
    public Account? Account { get; set; }
    public bool IsClosed { get; set; }
    public List<FolioCharge> Charges { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}

public class FolioCharge
{
    public int Id { get; set; }
    public int FolioId { get; set; }
    public Folio? Folio { get; set; }
    [MaxLength(150)] public string Description { get; set; } = "";
    public ChargeCategory Category { get; set; } = ChargeCategory.Room;
    public int Quantity { get; set; } = 1;
    [Column(TypeName = "decimal(12,2)")] public decimal UnitPrice { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal TaxRate { get; set; } = 10m;
    public DateTime Date { get; set; } = DateTime.Today;
    [NotMapped] public decimal Total => Quantity * UnitPrice;
}

public class PaymentMethod
{
    public int Id { get; set; }
    [MaxLength(50)] public string Name { get; set; } = "";
    [MaxLength(50)] public string NameEn { get; set; } = "";
}

public class CashRegister
{
    public int Id { get; set; }
    [MaxLength(60)] public string Name { get; set; } = "";
    [Column(TypeName = "decimal(14,2)")] public decimal Balance { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int FolioId { get; set; }
    public Folio? Folio { get; set; }
    public int PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public int? CashRegisterId { get; set; }
    public CashRegister? CashRegister { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    [MaxLength(100)] public string? Reference { get; set; }
    public int? UserId { get; set; }
}

public class Invoice
{
    public int Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = "";
    public int FolioId { get; set; }
    public Folio? Folio { get; set; }
    public int? AccountId { get; set; }
    public Account? Account { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    [Column(TypeName = "decimal(12,2)")] public decimal Subtotal { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal Tax { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal Total { get; set; }
    public bool IsEInvoice { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Issued";
    public List<InvoiceLine> Lines { get; set; } = new();
}

public class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    [MaxLength(150)] public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
    [Column(TypeName = "decimal(12,2)")] public decimal UnitPrice { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal TaxRate { get; set; }
}

public class DayClosing
{
    public int Id { get; set; }
    public DateTime BusinessDate { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal TotalRevenue { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal TotalPayments { get; set; }
    public int OccupiedRooms { get; set; }
    public int TotalRooms { get; set; }
    public DateTime ClosedAt { get; set; } = DateTime.Now;
    public int? ClosedByUserId { get; set; }
}

// ───────── Operasyon ─────────
public class Employee
{
    public int Id { get; set; }
    [MaxLength(120)] public string FullName { get; set; } = "";
    [MaxLength(60)] public string Department { get; set; } = "";
    [MaxLength(30)] public string? Phone { get; set; }
    public DateTime HireDate { get; set; } = DateTime.Today;
    public bool IsActive { get; set; } = true;
}

public class Shift
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}

public class HousekeepingTask
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public TaskType Type { get; set; } = TaskType.DailyClean;
    public WorkStatus Status { get; set; } = WorkStatus.Open;
    public DateTime Date { get; set; } = DateTime.Today;
    [MaxLength(300)] public string? Notes { get; set; }
}

public class MaintenanceRequest
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    [MaxLength(120)] public string Title { get; set; } = "";
    [MaxLength(500)] public string? Description { get; set; }
    public Priority Priority { get; set; } = Priority.Normal;
    public WorkStatus Status { get; set; } = WorkStatus.Open;
    public DateTime ReportedAt { get; set; } = DateTime.Now;
    public DateTime? ResolvedAt { get; set; }
}

public class ExtraService
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = "";
    public ChargeCategory Category { get; set; } = ChargeCategory.Other;
    [Column(TypeName = "decimal(10,2)")] public decimal Price { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal TaxRate { get; set; } = 10m;
    public bool IsActive { get; set; } = true;
}

public class StockItem
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(20)] public string Unit { get; set; } = "adet";
    public int Quantity { get; set; }
    public int MinQuantity { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal UnitCost { get; set; }
}

public class StockMovement
{
    public int Id { get; set; }
    public int StockItemId { get; set; }
    public StockItem? StockItem { get; set; }
    public StockMoveType Type { get; set; }
    public int Quantity { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    [MaxLength(200)] public string? Note { get; set; }
}

// ───────── İletişim / KBS ─────────
public class MessageTemplate
{
    public int Id { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    public MsgType Type { get; set; }
    public MsgTrigger Trigger { get; set; }
    [MaxLength(5)] public string Language { get; set; } = "tr";
    [MaxLength(150)] public string? Subject { get; set; }
    [MaxLength(1000)] public string Body { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class MessageLog
{
    public int Id { get; set; }
    public int? GuestId { get; set; }
    public Guest? Guest { get; set; }
    public int? ReservationId { get; set; }
    public MsgType Type { get; set; }
    [MaxLength(150)] public string To { get; set; } = "";
    [MaxLength(150)] public string? Subject { get; set; }
    [MaxLength(1000)] public string Body { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.Now;
    [MaxLength(20)] public string Status { get; set; } = "Sent";
}

public class KbsNotification
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    public int GuestId { get; set; }
    public Guest? Guest { get; set; }
    [MaxLength(20)] public string Authority { get; set; } = "Polis";
    [MaxLength(20)] public string Status { get; set; } = "Sent"; // Sent / Failed / CheckedOut
    public DateTime SentAt { get; set; } = DateTime.Now;
    [MaxLength(200)] public string? Response { get; set; }
}

// ───────── Sistem ─────────
public class AppUser
{
    public int Id { get; set; }
    [MaxLength(50)] public string Username { get; set; } = "";
    [MaxLength(200)] public string PasswordHash { get; set; } = "";
    [MaxLength(120)] public string FullName { get; set; } = "";
    [MaxLength(120)] public string? Email { get; set; }
    public AppRole Role { get; set; } = AppRole.FrontDesk;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    [MaxLength(60)] public string Username { get; set; } = "";
    [MaxLength(60)] public string Action { get; set; } = "";
    [MaxLength(60)] public string Entity { get; set; } = "";
    [MaxLength(40)] public string? EntityId { get; set; }
    [MaxLength(500)] public string? Details { get; set; }
    public DateTime At { get; set; } = DateTime.Now;
}

// ───────── Çevre & Aktiviteler (Barlar, Restoranlar, Turlar) ─────────
public class NearbyPlace
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public Hotel? Hotel { get; set; }
    [MaxLength(150)] public string Name { get; set; } = "";
    [MaxLength(50)] public string Category { get; set; } = "Restaurant"; // Bar, Restaurant, Activity
    [MaxLength(100)] public string SubCategory { get; set; } = "";
    public string Description { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public int DistanceMeters { get; set; }
    public int WalkingTimeMinutes { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    [MaxLength(300)] public string Address { get; set; } = "";
    [MaxLength(50)] public string Phone { get; set; } = "";
    public int PriceLevel { get; set; } = 2; // 1 to 4
    [Column(TypeName = "decimal(3,1)")] public decimal Rating { get; set; } = 4.8m;
    public int ReviewCount { get; set; } = 120;
    [MaxLength(100)] public string WorkingHours { get; set; } = "";
    [MaxLength(250)] public string? HotelDiscountNote { get; set; }
    [MaxLength(500)] public string? HighlightFeatures { get; set; }
    [MaxLength(500)] public string? Specialties { get; set; }
    [MaxLength(500)] public string? ImageUrl { get; set; }
    [MaxLength(500)] public string? GoogleMapsUrl { get; set; }
    [MaxLength(250)] public string? Website { get; set; }
    public bool IsPartner { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

// ───────── Otel Kariyer & Personel Arama (Garson, Komi, Temizlik vb.) ─────────
public enum JobRoleCategory
{
    Waiter,         // Garson (Servis Elemanı)
    Busboy,         // Komi (Garson Yardımcısı)
    Housekeeping,   // Temizlik / Kat Hizmetleri
    FrontDesk,      // Resepsiyon / Ön Büro
    Kitchen,        // Aşçı / Mutfak
    Bartender,      // Barmen / Barista
    Security,       // Güvenlik & Bellboy
    Other
}

public class JobPosting
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public Hotel? Hotel { get; set; }
    [MaxLength(150)] public string Title { get; set; } = "";
    public JobRoleCategory Category { get; set; } = JobRoleCategory.Waiter;
    [MaxLength(80)] public string PositionName { get; set; } = "Garson";
    [MaxLength(80)] public string City { get; set; } = "Bodrum";
    [MaxLength(100)] public string SalaryRange { get; set; } = "30.000 ₺ - 36.000 ₺ + Tip";
    [MaxLength(100)] public string WorkType { get; set; } = "Tam Zamanlı (Sezonluk)";
    public bool AccommodationProvided { get; set; } = true; // Lojman Sağlanır
    public bool MealsIncluded { get; set; } = true; // 3 Öğün Yemek
    [MaxLength(100)] public string ExperienceRequired { get; set; } = "En az 1 yıl veya Yetiştirilmek Üzere";
    public string Description { get; set; } = "";
    public string Requirements { get; set; } = "";
    [MaxLength(50)] public string ContactPhone { get; set; } = "+90 252 555 00 00";
    [MaxLength(100)] public string? ContactEmail { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<JobApplication> Applications { get; set; } = new();
}

public class JobApplication
{
    public int Id { get; set; }
    public int JobPostingId { get; set; }
    public JobPosting? JobPosting { get; set; }
    [MaxLength(100)] public string FullName { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    [MaxLength(100)] public string? Email { get; set; }
    [MaxLength(80)] public string CurrentCity { get; set; } = "";
    [MaxLength(100)] public string ExperienceYears { get; set; } = "1-2 Yıl";
    [MaxLength(50)] public string RoleApplied { get; set; } = "Garson";
    public bool NeedsAccommodation { get; set; } = true;
    [MaxLength(1000)] public string Notes { get; set; } = "";
    [MaxLength(30)] public string Status { get; set; } = "Beklemede"; // Beklemede / Arandı / Mülakat / Onaylandı
    public DateTime AppliedAt { get; set; } = DateTime.Now;
}

public class JobCandidatePool
{
    public int Id { get; set; }
    [MaxLength(100)] public string FullName { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    [MaxLength(80)] public string City { get; set; } = "";
    [MaxLength(80)] public string PreferredRole { get; set; } = "Komi";
    [MaxLength(100)] public string ExperienceYears { get; set; } = "1 Yıl";
    public bool NeedsAccommodation { get; set; } = true;
    public bool ReadyImmediate { get; set; } = true;
    [MaxLength(500)] public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}


