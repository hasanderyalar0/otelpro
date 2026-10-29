using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using Otel.Web.Data;

namespace Otel.Web.Services;

public class HotelState(ProtectedLocalStorage storage, IDbContextFactory<AppDb> dbFactory)
{
    private bool _initialized = false;
    public List<Hotel> Hotels { get; private set; } = new();
    public int SelectedHotelId { get; private set; } = 1;
    public Hotel? SelectedHotel => Hotels.FirstOrDefault(h => h.Id == SelectedHotelId) ?? Hotels.FirstOrDefault();

    public event Action? OnChange;

    public async Task InitAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        Hotels = await db.Hotels.AsNoTracking().OrderBy(h => h.Id).ToListAsync();

        if (Hotels.Count > 0 && SelectedHotelId == 0)
        {
            SelectedHotelId = Hotels[0].Id;
        }

        if (!_initialized && Hotels.Count > 0)
        {
            try
            {
                var r = await storage.GetAsync<int>("active_hotel_id");
                if (r.Success && Hotels.Any(h => h.Id == r.Value))
                {
                    SelectedHotelId = r.Value;
                }
                else
                {
                    SelectedHotelId = Hotels[0].Id;
                }
                _initialized = true;
            }
            catch
            {
                // JavaScript interop not yet ready in prerender
                if (SelectedHotelId == 0)
                {
                    SelectedHotelId = Hotels[0].Id;
                }
            }
        }
    }

    public async Task SetHotelAsync(int hotelId)
    {
        if (SelectedHotelId == hotelId) return;
        SelectedHotelId = hotelId;
        try
        {
            await storage.SetAsync("active_hotel_id", hotelId);
        }
        catch { }

        OnChange?.Invoke();
    }
}
