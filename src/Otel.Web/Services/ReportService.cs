using Microsoft.EntityFrameworkCore;
using Otel.Web.Data;

namespace Otel.Web.Services;

public class OccRow
{
    public DateTime Date { get; set; }
    public int TotalRooms { get; set; }
    public int OccupiedRooms { get; set; }
    public decimal RoomRevenue { get; set; }
    public decimal? OccupancyPct { get; set; }
    public decimal? ADR { get; set; }
    public decimal? RevPAR { get; set; }
}

public record ChannelShare(string Channel, int Count, decimal Revenue);
public record DashStats(int Arrivals, int Departures, int InHouse, int TotalRooms, decimal OccupancyToday, decimal Adr30, decimal RevPar30, decimal Revenue30);

public class ReportService(IDbContextFactory<AppDb> dbFactory)
{
    public async Task<List<OccRow>> OccupancyAsync(int? hotelId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var today = DateTime.Today;
        var start = today.AddDays(-30);
        var end = today.AddDays(30);

        int totalRooms;
        if (hotelId.HasValue && hotelId.Value > 0)
        {
            totalRooms = await db.Rooms.CountAsync(r => r.HotelId == hotelId.Value);
            if (totalRooms == 0) totalRooms = 10;
        }
        else
        {
            totalRooms = await db.Rooms.CountAsync();
            if (totalRooms == 0) totalRooms = 40;
        }

        var resQuery = db.ReservationRooms
            .Include(rr => rr.Reservation)
            .Include(rr => rr.Room)
            .Include(rr => rr.RoomType)
            .Where(rr => rr.Reservation != null
                && rr.Reservation.Status != ResStatus.Cancelled
                && rr.Reservation.Status != ResStatus.NoShow
                && rr.CheckIn < end && rr.CheckOut > start);

        if (hotelId.HasValue && hotelId.Value > 0)
        {
            resQuery = resQuery.Where(rr => (rr.Room != null && rr.Room.HotelId == hotelId.Value) || (rr.RoomType != null && rr.RoomType.HotelId == hotelId.Value));
        }

        var activeBookings = await resQuery.ToListAsync();

        var rows = new List<OccRow>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var onDay = activeBookings.Where(b => b.CheckIn <= d && b.CheckOut > d).ToList();
            int occCount = onDay.Count;
            decimal dayRev = onDay.Sum(b => b.NightlyRate);
            decimal occPct = totalRooms > 0 ? Math.Min(100m, Math.Round((decimal)occCount * 100m / totalRooms, 1)) : 0;
            decimal adr = occCount > 0 ? Math.Round(dayRev / occCount, 2) : 0;
            decimal revpar = totalRooms > 0 ? Math.Round(dayRev / totalRooms, 2) : 0;

            rows.Add(new OccRow
            {
                Date = d,
                TotalRooms = totalRooms,
                OccupiedRooms = occCount,
                RoomRevenue = dayRev,
                OccupancyPct = occPct,
                ADR = adr,
                RevPAR = revpar
            });
        }

        return rows;
    }

    public async Task<DashStats> StatsAsync(int? hotelId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var today = DateTime.Today;
        var occ = await OccupancyAsync(hotelId);
        var past = occ.Where(o => o.Date <= today && o.Date > today.AddDays(-30)).ToList();
        var t = occ.FirstOrDefault(o => o.Date == today);
        var rev = past.Sum(o => o.RoomRevenue);
        var occRooms = past.Sum(o => o.OccupiedRooms);
        var tot = past.Sum(o => o.TotalRooms);

        var resQ = db.Reservations
            .Include(r => r.Rooms).ThenInclude(rr => rr.Room)
            .Include(r => r.Rooms).ThenInclude(rr => rr.RoomType)
            .Where(r => !hotelId.HasValue || r.Rooms.Any(rr => (rr.Room != null && rr.Room.HotelId == hotelId.Value) || (rr.RoomType != null && rr.RoomType.HotelId == hotelId.Value)))
            .AsQueryable();

        int arrivals = await resQ.CountAsync(r => r.CheckIn == today && (r.Status == ResStatus.Confirmed || r.Status == ResStatus.CheckedIn));
        int departures = await resQ.CountAsync(r => r.CheckOut == today && (r.Status == ResStatus.CheckedIn || r.Status == ResStatus.CheckedOut));
        int inHouse = await resQ.CountAsync(r => r.Status == ResStatus.CheckedIn);
        int totalRooms = t?.TotalRooms ?? 10;
        decimal occToday = t?.OccupancyPct ?? 0;

        return new DashStats(
            arrivals,
            departures,
            inHouse,
            totalRooms,
            occToday,
            occRooms == 0 ? 0 : Math.Round(rev / occRooms, 2),
            tot == 0 ? 0 : Math.Round(rev / tot, 2),
            rev);
    }

    public async Task<List<ChannelShare>> ChannelMixAsync(int? hotelId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Reservations
            .Where(r => r.Status != ResStatus.Cancelled && r.Status != ResStatus.NoShow)
            .Where(r => !hotelId.HasValue || r.Rooms.Any(rr => (rr.Room != null && rr.Room.HotelId == hotelId.Value) || (rr.RoomType != null && rr.RoomType.HotelId == hotelId.Value)))
            .AsQueryable();

        var list = await q.GroupBy(r => r.Channel!.Name ?? "Direkt")
            .Select(g => new ChannelShare(g.Key, g.Count(), g.Sum(x => x.TotalAmount)))
            .ToListAsync();

        return list.OrderByDescending(x => x.Revenue).ToList();
    }
}
