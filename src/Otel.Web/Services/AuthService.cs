using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using Otel.Web.Data;

namespace Otel.Web.Services;

/// <summary>Devre (circuit) başına oturum; ProtectedSessionStorage ile sayfa yenilemesinde korunur.</summary>
public class AuthService(AppDb db, ProtectedSessionStorage storage)
{
    public AppUser? User { get; private set; }
    public bool Initialized { get; private set; }
    public bool IsAuthenticated => User != null;
    public event Action? Changed;

    public async Task InitAsync()
    {
        if (Initialized) return;
        try
        {
            var r = await storage.GetAsync<int>("uid");
            if (r.Success) User = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == r.Value && u.IsActive);
        }
        catch { /* JS henüz hazır değil */ }
        Initialized = true;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Username == username && x.IsActive);
        if (u == null || !PasswordHasher.Verify(password, u.PasswordHash)) return false;
        User = u;
        await storage.SetAsync("uid", u.Id);
        db.AuditLogs.Add(new AuditLog { UserId = u.Id, Username = u.Username, Action = "Login", Entity = "User", EntityId = u.Id.ToString() });
        await db.SaveChangesAsync();
        Changed?.Invoke();
        return true;
    }

    public async Task LogoutAsync()
    {
        User = null;
        await storage.DeleteAsync("uid");
        Changed?.Invoke();
    }

    public bool InRole(params AppRole[] roles) => User != null && (User.Role == AppRole.Admin || roles.Contains(User.Role));

    public async Task AuditAsync(string action, string entity, string? id = null, string? details = null)
    {
        db.AuditLogs.Add(new AuditLog { UserId = User?.Id, Username = User?.Username ?? "system", Action = action, Entity = entity, EntityId = id, Details = details });
        await db.SaveChangesAsync();
    }
}
