using booking_api.Data;
using booking_api.DTOs;
using booking_api.Models;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly AppDbContext _db;
    private readonly IS3Service _s3;

    public AvailabilityService(AppDbContext db, IS3Service s3)
    {
        _db = db;
        _s3 = s3;
    }

    public async Task<AvailabilityResponse> GetAsync(Guid gameId, DateTime fromUtc, int days, CancellationToken ct = default)
    {
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == gameId, ct)
            ?? throw new KeyNotFoundException("Game not found.");

        var from = DateTime.SpecifyKind(new DateTime(fromUtc.Year, fromUtc.Month, fromUtc.Day, fromUtc.Hour, 0, 0), DateTimeKind.Utc);
        var to = from.AddDays(Math.Clamp(days, 1, 31));

        var rooms = await _db.Rooms
            .Where(r => r.GameId == gameId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        var roomIds = rooms.Select(r => r.Id).ToList();

        var windows = await _db.RoomStatusWindows
            .Where(w => roomIds.Contains(w.RoomId) && w.StartTime < to && w.EndTime > from)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        var bookings = await _db.Bookings
            .Where(b => roomIds.Contains(b.RoomId)
                && b.Type == BookingType.Regular
                && b.StartTime < to && b.EndTime > from
                && (b.Status == BookingStatus.Approved
                    || b.Status == BookingStatus.ProofSubmitted
                    || (b.Status == BookingStatus.PendingPayment && b.HoldExpiresAt != null && b.HoldExpiresAt > now)))
            .Select(b => new { b.RoomId, b.StartTime, b.EndTime })
            .ToListAsync(ct);

        var queueLengths = await _db.QueueEntries
            .Where(q => q.State == QueueState.Queued && roomIds.Contains(q.Window.RoomId))
            .GroupBy(q => q.WindowId)
            .Select(g => new { WindowId = g.Key, Count = g.Sum(x => x.Party.Size) })
            .ToDictionaryAsync(x => x.WindowId, x => x.Count, ct);

        var roomDtos = new List<RoomAvailabilityDto>(rooms.Count);

        foreach (var room in rooms)
        {
            var roomWindows = windows.Where(w => w.RoomId == room.Id).OrderBy(w => w.StartTime).ToList();
            var roomBookings = bookings.Where(b => b.RoomId == room.Id).ToList();

            var slots = new List<RoomSlotDto>();
            for (var t = from; t < to; t = t.AddHours(1))
            {
                var slotEnd = t.AddHours(1);
                var window = roomWindows.FirstOrDefault(w => w.StartTime <= t && w.EndTime >= slotEnd);

                var status = window?.Status ?? RoomStatus.Open;
                bool available;
                int? queueLen = null;

                switch (status)
                {
                    case RoomStatus.Closed:
                    case RoomStatus.Maintenance:
                    case RoomStatus.Tournament:
                        available = false;
                        break;
                    case RoomStatus.OpenPlay:
                        available = true;
                        queueLen = window != null && queueLengths.TryGetValue(window.Id, out var c) ? c : 0;
                        break;
                    default:
                        available = !roomBookings.Any(b => b.StartTime < slotEnd && b.EndTime > t);
                        break;
                }

                slots.Add(new RoomSlotDto(
                    t,
                    slotEnd,
                    status,
                    available,
                    window?.Id,
                    window?.SeatRate,
                    window?.MatchSize,
                    queueLen
                ));
            }

            roomDtos.Add(new RoomAvailabilityDto(
                await RoomMapper.ToDtoAsync(room, _s3, ct),
                slots
            ));
        }

        return new AvailabilityResponse(
            new GameDto(game.Id, game.Name, game.Description, game.IconUrl),
            from,
            to,
            roomDtos
        );
    }
}
