using Microsoft.EntityFrameworkCore;
using Otel.Web.Data;

namespace Otel.Web.Services;

public class MessagingService(AppDb db)
{
    public async Task SendTemplateAsync(int reservationId, MsgTrigger trigger, string lang = "tr")
    {
        var res = await db.Reservations.Include(r => r.Guest).FirstOrDefaultAsync(r => r.Id == reservationId);
        if (res?.Guest == null) return;
        var templates = await db.MessageTemplates.Where(t => t.Trigger == trigger && t.IsActive && t.Language == (res.Guest.Nationality == "TR" ? "tr" : "en")).ToListAsync();
        if (templates.Count == 0) templates = await db.MessageTemplates.Where(t => t.Trigger == trigger && t.IsActive).ToListAsync();
        foreach (var t in templates.Take(1))
        {
            db.MessageLogs.Add(new MessageLog
            {
                GuestId = res.GuestId, ReservationId = res.Id, Type = t.Type,
                To = t.Type == MsgType.Sms ? res.Guest.Phone ?? "-" : res.Guest.Email ?? "-",
                Subject = t.Subject, Body = Render(t.Body, res)
            });
        }
        await db.SaveChangesAsync();
    }

    public static string Render(string body, Reservation r) => body
        .Replace("{Ad}", r.Guest?.FullName).Replace("{Kod}", r.Code)
        .Replace("{Giris}", r.CheckIn.ToString("dd.MM.yyyy")).Replace("{Cikis}", r.CheckOut.ToString("dd.MM.yyyy"));
}
