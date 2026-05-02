using booking_api.Data;
using booking_api.DTOs;
using booking_api.Models;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Services;

public class DisplayService : IDisplayService
{
    private readonly AppDbContext _db;

    public DisplayService(AppDbContext db) => _db = db;

    public async Task<DisplaySnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var horizon = now.AddHours(24);

        var rooms = await _db.Rooms
            .Include(r => r.Game)
            .OrderBy(r => r.Game.Name).ThenBy(r => r.Name)
            .ToListAsync(ct);

        var roomIds = rooms.Select(r => r.Id).ToList();

        var windows = await _db.RoomStatusWindows
            .Where(w => roomIds.Contains(w.RoomId) && w.StartTime < horizon && w.EndTime > now)
            .ToListAsync(ct);

        var bookings = await _db.Bookings
            .Include(b => b.BookedByUser)
            .Where(b => roomIds.Contains(b.RoomId)
                && b.Type == BookingType.Regular
                && b.Status == BookingStatus.Approved
                && b.StartTime < horizon && b.EndTime > now)
            .ToListAsync(ct);

        var matches = await _db.Matches
            .Include(m => m.Players).ThenInclude(p => p.User)
            .Where(m => roomIds.Contains(m.RoomId) && m.EndedAt == null)
            .ToListAsync(ct);

        var queueCounts = await _db.QueueEntries
            .Where(q => q.State == QueueState.Queued && roomIds.Contains(q.Window.RoomId))
            .GroupBy(q => q.Window.RoomId)
            .Select(g => new { RoomId = g.Key, Count = g.Sum(x => x.Party.Size) })
            .ToDictionaryAsync(x => x.RoomId, x => x.Count, ct);

        var states = new List<DisplayRoomState>(rooms.Count);

        foreach (var room in rooms)
        {
            var activeWindow = windows
                .Where(w => w.RoomId == room.Id && w.StartTime <= now && w.EndTime > now)
                .OrderByDescending(w => w.StartTime)
                .FirstOrDefault();

            var nextWindow = windows
                .Where(w => w.RoomId == room.Id && w.StartTime > now)
                .OrderBy(w => w.StartTime)
                .FirstOrDefault();

            var currentBooking = bookings
                .Where(b => b.RoomId == room.Id && b.StartTime <= now && b.EndTime > now)
                .FirstOrDefault();

            var nextBooking = bookings
                .Where(b => b.RoomId == room.Id && b.StartTime > now)
                .OrderBy(b => b.StartTime)
                .FirstOrDefault();

            var status = activeWindow?.Status ?? RoomStatus.Open;
            string? currentLabel = null;
            DateTime? currentEnds = null;
            string? nextLabel = null;
            DateTime? nextStarts = null;
            int? queueLen = null;
            int? activeMatches = null;
            Guid? currentMatchId = null;
            IReadOnlyList<MatchPlayerDto>? currentMatchPlayers = null;

            if (currentBooking is not null && status == RoomStatus.Open)
            {
                currentLabel = $"Reserved · {Initials(currentBooking.BookedByUser)}";
                currentEnds = currentBooking.EndTime;
            }
            else if (status == RoomStatus.OpenPlay)
            {
                queueLen = queueCounts.GetValueOrDefault(room.Id);
                var roomMatches = matches.Where(m => m.RoomId == room.Id).ToList();
                activeMatches = roomMatches.Count;
                var current = roomMatches.OrderBy(m => m.StartedAt).FirstOrDefault();
                if (current is not null)
                {
                    currentMatchId = current.Id;
                    currentMatchPlayers = current.Players
                        .Select(p => new MatchPlayerDto(p.UserId, $"{p.User.FirstName} {p.User.LastName}".Trim(), p.PartyId))
                        .ToList();
                    currentLabel = "Match in progress";
                }
                else
                {
                    currentLabel = $"Open Play · {queueLen} in queue";
                }
                currentEnds = activeWindow?.EndTime;
            }
            else if (status != RoomStatus.Open)
            {
                currentLabel = status.ToString();
                currentEnds = activeWindow?.EndTime;
            }

            DateTime? candidate = null;
            if (nextBooking is not null) candidate = nextBooking.StartTime;
            if (nextWindow is not null && (candidate is null || nextWindow.StartTime < candidate))
                candidate = nextWindow.StartTime;

            if (candidate is not null)
            {
                nextStarts = candidate;
                if (nextBooking is not null && nextBooking.StartTime == candidate)
                    nextLabel = $"Reserved · {Initials(nextBooking.BookedByUser)}";
                else if (nextWindow is not null)
                    nextLabel = nextWindow.Status.ToString();
            }

            states.Add(new DisplayRoomState(
                room.Id, room.Name, room.GameId, room.Game.Name,
                status, currentLabel, currentEnds, nextStarts, nextLabel,
                queueLen, activeMatches, currentMatchId, currentMatchPlayers
            ));
        }

        return new DisplaySnapshot(now, states);
    }

    private static string Initials(User? u)
    {
        if (u is null) return "—";
        var f = string.IsNullOrEmpty(u.FirstName) ? "" : u.FirstName[..1].ToUpper();
        var l = string.IsNullOrEmpty(u.LastName) ? "" : u.LastName[..1].ToUpper();
        return $"{f}{l}";
    }
}
