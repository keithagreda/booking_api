using booking_api.Data;
using booking_api.Models;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Services;

public class MatchmakingService : IMatchmakingService
{
    private readonly AppDbContext _db;

    public MatchmakingService(AppDbContext db) => _db = db;

    public async Task TryFormMatchesAsync(Guid windowId, CancellationToken ct = default)
    {
        var window = await _db.RoomStatusWindows.FirstOrDefaultAsync(w => w.Id == windowId, ct);
        if (window is null || window.Status != RoomStatus.OpenPlay || window.MatchSize is null)
            return;

        var matchSize = window.MatchSize.Value;
        var now = DateTime.UtcNow;
        if (window.EndTime <= now)
            return;

        while (true)
        {
            var entries = await _db.QueueEntries
                .Where(q => q.WindowId == windowId && q.State == QueueState.Queued)
                .Include(q => q.Party)
                .OrderBy(q => q.EnqueuedAt)
                .ToListAsync(ct);

            var picked = PickMatch(entries, matchSize);
            if (picked is null)
                return;

            var match = new Match
            {
                WindowId = windowId,
                RoomId = window.RoomId,
                StartedAt = DateTime.UtcNow
            };
            _db.Matches.Add(match);

            var bookingsByParty = await _db.Bookings
                .Where(b => picked.Select(e => e.PartyId).Contains(b.PartyId!.Value))
                .ToListAsync(ct);

            foreach (var entry in picked)
            {
                entry.State = QueueState.InMatch;
                entry.CurrentMatchId = match.Id;

                var partyBookings = bookingsByParty.Where(b => b.PartyId == entry.PartyId);
                foreach (var booking in partyBookings)
                {
                    _db.MatchPlayers.Add(new MatchPlayer
                    {
                        MatchId = match.Id,
                        BookingId = booking.Id,
                        UserId = booking.BookedByUserId,
                        PartyId = entry.PartyId
                    });
                }
            }

            await _db.SaveChangesAsync(ct);
        }
    }

    private static List<QueueEntry>? PickMatch(List<QueueEntry> queue, int matchSize)
    {
        var picked = new List<QueueEntry>();
        var remaining = matchSize;

        foreach (var entry in queue)
        {
            var size = entry.Party.Size;
            if (size <= remaining)
            {
                picked.Add(entry);
                remaining -= size;
                if (remaining == 0)
                    return picked;
            }
        }

        return null;
    }
}
