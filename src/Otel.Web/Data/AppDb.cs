using Microsoft.EntityFrameworkCore;

namespace Otel.Web.Data;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<RoomFeature> RoomFeatures => Set<RoomFeature>();
    public DbSet<RoomTypeFeature> RoomTypeFeatures => Set<RoomTypeFeature>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<GuestDocument> GuestDocuments => Set<GuestDocument>();
    public DbSet<BlacklistEntry> Blacklist => Set<BlacklistEntry>();
    public DbSet<GuestNote> GuestNotes => Set<GuestNote>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<BoardType> BoardTypes => Set<BoardType>();
    public DbSet<RatePlan> RatePlans => Set<RatePlan>();
    public DbSet<SeasonalRate> SeasonalRates => Set<SeasonalRate>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<ChannelInventory> ChannelInventory => Set<ChannelInventory>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationRoom> ReservationRooms => Set<ReservationRoom>();
    public DbSet<ReservationGuest> ReservationGuests => Set<ReservationGuest>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Folio> Folios => Set<Folio>();
    public DbSet<FolioCharge> FolioCharges => Set<FolioCharge>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<DayClosing> DayClosings => Set<DayClosing>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<HousekeepingTask> HousekeepingTasks => Set<HousekeepingTask>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<ExtraService> ExtraServices => Set<ExtraService>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();
    public DbSet<KbsNotification> KbsNotifications => Set<KbsNotification>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<NearbyPlace> NearbyPlaces => Set<NearbyPlace>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<JobCandidatePool> JobCandidates => Set<JobCandidatePool>();


    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ReservationRoom>().ToTable(t => t.HasTrigger("trg_ReservationRooms_NoOverlap"));
        b.Entity<Payment>().ToTable(t => t.HasTrigger("trg_Payments_UpdateCash"));
        b.Entity<StockMovement>().ToTable(t => t.HasTrigger("trg_StockMovements_UpdateQty"));
        b.Entity<Reservation>().ToTable(t => t.HasTrigger("trg_Reservations_Audit"));

        b.Entity<RoomTypeFeature>().HasKey(x => new { x.RoomTypeId, x.FeatureId });
        b.Entity<ReservationGuest>().HasKey(x => new { x.ReservationId, x.GuestId });

        b.Entity<Room>().HasIndex(r => new { r.HotelId, r.Number }).IsUnique();
        b.Entity<Reservation>().HasIndex(r => r.Code).IsUnique();
        b.Entity<Reservation>().HasIndex(r => new { r.CheckIn, r.CheckOut });
        b.Entity<ReservationRoom>().HasIndex(r => new { r.RoomId, r.CheckIn, r.CheckOut });
        b.Entity<Guest>().HasIndex(g => g.LastName);
        b.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        b.Entity<Channel>().HasIndex(c => c.Code).IsUnique();
        b.Entity<ChannelInventory>().HasIndex(c => new { c.ChannelId, c.RoomTypeId, c.Date }).IsUnique();
        b.Entity<Invoice>().HasIndex(i => i.Number).IsUnique();
        b.Entity<DayClosing>().HasIndex(d => d.BusinessDate).IsUnique();

        // Enum → string (okunabilir veri)
        foreach (var et in b.Model.GetEntityTypes())
            foreach (var p in et.GetProperties().Where(p => p.ClrType.IsEnum))
                p.SetProviderClrType(typeof(string));

        // Döngüsel cascade yollarını engelle
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        b.Entity<ReservationRoom>().HasOne(x => x.Reservation).WithMany(r => r.Rooms).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ReservationGuest>().HasOne(x => x.Reservation).WithMany(r => r.Guests).OnDelete(DeleteBehavior.Cascade);
        b.Entity<FolioCharge>().HasOne(x => x.Folio).WithMany(f => f.Charges).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Payment>().HasOne(x => x.Folio).WithMany(f => f.Payments).OnDelete(DeleteBehavior.Cascade);
        b.Entity<InvoiceLine>().HasOne(x => x.Invoice).WithMany(i => i.Lines).OnDelete(DeleteBehavior.Cascade);
        b.Entity<GuestDocument>().HasOne(x => x.Guest).WithMany(g => g.Documents).OnDelete(DeleteBehavior.Cascade);
    }
}
