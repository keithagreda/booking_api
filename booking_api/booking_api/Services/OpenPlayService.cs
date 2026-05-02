using booking_api.Data;
using booking_api.DTOs;
using booking_api.Models;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Services;

public class OpenPlayService : IOpenPlayService
{
    private readonly AppDbContext _db;
    private readonly IMatchmakingService _matchmaker;
    private readonly ILiveBroadcaster _broadcaster;
    private const int HoldMinutes = 15;

    public OpenPlayService(AppDbContext db, IMatchmakingService matchmaker, ILiveBroadcaster broadcaster)
    {
        _db = db;
        _matchmaker = matchmaker;
        _broadcaster = broadcaster;
    }

    public async Task<JoinOpenPlayResponse> JoinAsync(Guid windowId, Guid userId, Guid? partnerUserId, CancellationToken ct = default)
    {
        var window = await _db.RoomStatusWindows
            .FirstOrDefaultAsync(w => w.Id == windowId, ct)
            ?? throw new KeyNotFoundException("Window not found.");

        if (window.Status != RoomStatus.OpenPlay)
            throw new InvalidOperationException("Window is not open play.");

        if (window.SeatRate is null || window.MatchSize is null)
            throw new InvalidOperationException("Open-play window is missing rate or match size.");

        var now = DateTime.UtcNow;
        if (window.EndTime <= now)
            throw new InvalidOperationException("Open-play window has ended.");

        if (partnerUserId == userId)
            throw new ArgumentException("Partner cannot be yourself.");

        var alreadyIn = await _db.Parties.AnyAsync(p =>
            p.WindowId == windowId
            && p.State != PartyState.Cancelled
            && (p.LeaderUserId == userId || p.PartnerUserId == userId), ct);
        if (alreadyIn)
            throw new InvalidOperationException("You're already in this open-play window.");

        if (window.QueueCap is not null)
        {
            var inQueue = await _db.QueueEntries
                .Where(q => q.WindowId == windowId && q.State == QueueState.Queued)
                .SumAsync(q => (int?)q.Party.Size, ct) ?? 0;
            var partySize = partnerUserId is null ? 1 : 2;
            if (inQueue + partySize > window.QueueCap)
                throw new InvalidOperationException("Queue is full.");
        }

        var party = new Party
        {
            WindowId = windowId,
            LeaderUserId = userId,
            PartnerUserId = partnerUserId,
            Size = partnerUserId is null ? 1 : 2,
            State = partnerUserId is null ? PartyState.Confirmed : PartyState.PendingPartner
        };
        _db.Parties.Add(party);

        var bookings = new List<Booking>();
        bookings.Add(CreateSeatBooking(window, userId, party));

        if (partnerUserId is not null)
            bookings.Add(CreateSeatBooking(window, partnerUserId.Value, party));

        foreach (var b in bookings)
            _db.Bookings.Add(b);

        await _db.SaveChangesAsync(ct);

        await BroadcastWindowAsync(windowId, ct);

        var dtos = new List<BookingDto>(bookings.Count);
        foreach (var b in bookings)
            dtos.Add(BuildBookingDto(b, window.Id));

        return new JoinOpenPlayResponse(party.Id, dtos);
    }

    private Booking CreateSeatBooking(RoomStatusWindow window, Guid userId, Party party)
    {
        var amount = window.SeatRate!.Value;
        var booking = new Booking
        {
            RoomId = window.RoomId,
            BookedByUserId = userId,
            StartTime = window.StartTime,
            EndTime = window.EndTime,
            Type = BookingType.OpenPlaySeat,
            Status = BookingStatus.PendingPayment,
            TotalAmount = amount,
            HoldExpiresAt = DateTime.UtcNow.AddMinutes(HoldMinutes),
            PartyId = party.Id,
            WindowId = window.Id,
            Notes = null
        };
        booking.Payment = new Payment
        {
            BookingId = booking.Id,
            Method = PaymentMethod.GCash,
            Status = PaymentStatus.AwaitingProof,
            Amount = amount
        };
        return booking;
    }

    private static BookingDto BuildBookingDto(Booking b, Guid windowId)
    {
        var p = b.Payment!;
        return new BookingDto(
            b.Id, b.RoomId, "", b.BookedByUserId, b.Type, b.Status,
            b.StartTime, b.EndTime, b.TotalAmount, b.HoldExpiresAt, b.Notes,
            new PaymentDto(p.Id, p.BookingId, p.Method, p.Status, p.Amount,
                p.GcashReference, p.ProofS3Key, null, p.RejectionReason, p.ReviewedAt)
        );
    }

    public async Task<OpenPlayWindowState> AcceptPartyAsync(Guid partyId, Guid userId, CancellationToken ct = default)
    {
        var party = await _db.Parties.FirstOrDefaultAsync(p => p.Id == partyId, ct)
            ?? throw new KeyNotFoundException("Party not found.");

        if (party.PartnerUserId != userId)
            throw new UnauthorizedAccessException("Not the partner on this party.");

        if (party.State != PartyState.PendingPartner)
            throw new InvalidOperationException($"Party is in state {party.State}.");

        party.State = PartyState.Confirmed;
        await _db.SaveChangesAsync(ct);

        await TryEnqueuePartyAsync(party.Id, ct);
        await BroadcastWindowAsync(party.WindowId, ct);
        return await GetStateAsync(party.WindowId, ct);
    }

    public async Task<OpenPlayWindowState> LeaveAsync(Guid windowId, Guid userId, CancellationToken ct = default)
    {
        var party = await _db.Parties
            .FirstOrDefaultAsync(p => p.WindowId == windowId
                && p.State != PartyState.Cancelled
                && (p.LeaderUserId == userId || p.PartnerUserId == userId), ct);

        if (party is null)
            return await GetStateAsync(windowId, ct);

        var queueEntry = await _db.QueueEntries
            .FirstOrDefaultAsync(q => q.PartyId == party.Id && q.State != QueueState.Left, ct);
        if (queueEntry is not null)
            queueEntry.State = QueueState.Left;

        party.State = PartyState.Cancelled;

        var bookings = await _db.Bookings
            .Where(b => b.PartyId == party.Id && b.Status == BookingStatus.PendingPayment)
            .ToListAsync(ct);
        foreach (var b in bookings)
            b.Status = BookingStatus.Cancelled;

        await _db.SaveChangesAsync(ct);
        await BroadcastWindowAsync(windowId, ct);
        return await GetStateAsync(windowId, ct);
    }

    public async Task OnSeatPaymentApprovedAsync(Guid bookingId, CancellationToken ct = default)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, ct);
        if (booking is null || booking.Type != BookingType.OpenPlaySeat || booking.PartyId is null)
            return;

        await TryEnqueuePartyAsync(booking.PartyId.Value, ct);
        if (booking.WindowId is not null)
            await BroadcastWindowAsync(booking.WindowId.Value, ct);
    }

    private async Task TryEnqueuePartyAsync(Guid partyId, CancellationToken ct)
    {
        var party = await _db.Parties.FirstOrDefaultAsync(p => p.Id == partyId, ct);
        if (party is null || party.State != PartyState.Confirmed)
            return;

        var existing = await _db.QueueEntries.AnyAsync(q => q.PartyId == partyId && q.State != QueueState.Left, ct);
        if (existing)
            return;

        var bookings = await _db.Bookings.Where(b => b.PartyId == partyId).ToListAsync(ct);
        if (bookings.Count != party.Size)
            return;

        if (bookings.Any(b => b.Status != BookingStatus.Approved))
            return;

        _db.QueueEntries.Add(new QueueEntry
        {
            WindowId = party.WindowId,
            PartyId = party.Id,
            EnqueuedAt = DateTime.UtcNow,
            State = QueueState.Queued
        });
        await _db.SaveChangesAsync(ct);

        await _matchmaker.TryFormMatchesAsync(party.WindowId, ct);
    }

    public async Task<MatchDto> EndMatchAsync(Guid matchId, Guid adminUserId, CancellationToken ct = default)
    {
        var match = await _db.Matches
            .Include(m => m.Players)
            .FirstOrDefaultAsync(m => m.Id == matchId, ct)
            ?? throw new KeyNotFoundException("Match not found.");

        if (match.EndedAt is not null)
            throw new InvalidOperationException("Match already ended.");

        match.EndedAt = DateTime.UtcNow;
        match.EndedByUserId = adminUserId;

        var partyIds = match.Players.Where(p => p.PartyId is not null).Select(p => p.PartyId!.Value).Distinct().ToList();
        var queueEntries = await _db.QueueEntries
            .Where(q => q.CurrentMatchId == match.Id)
            .ToListAsync(ct);

        foreach (var q in queueEntries)
        {
            q.State = QueueState.Queued;
            q.CurrentMatchId = null;
            q.EnqueuedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        await _matchmaker.TryFormMatchesAsync(match.WindowId, ct);
        await BroadcastWindowAsync(match.WindowId, ct);

        return await BuildMatchDtoAsync(match.Id, ct);
    }

    public async Task<IReadOnlyList<OpenPlayWindowSummary>> ListLiveAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var windows = await _db.RoomStatusWindows
            .Include(w => w.Room)
            .ThenInclude(r => r.Game)
            .Where(w => w.Status == RoomStatus.OpenPlay && w.EndTime > now)
            .OrderBy(w => w.StartTime)
            .ToListAsync(ct);

        var ids = windows.Select(w => w.Id).ToList();
        var queueCounts = await _db.QueueEntries
            .Where(q => ids.Contains(q.WindowId) && q.State == QueueState.Queued)
            .GroupBy(q => q.WindowId)
            .Select(g => new { g.Key, Count = g.Sum(x => x.Party.Size) })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var matchCounts = await _db.Matches
            .Where(m => ids.Contains(m.WindowId) && m.EndedAt == null)
            .GroupBy(m => m.WindowId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return windows.Select(w => new OpenPlayWindowSummary(
            w.Id, w.RoomId, w.Room.Name, w.Room.GameId, w.Room.Game.Name,
            w.StartTime, w.EndTime, w.SeatRate ?? 0, w.MatchSize ?? 0, w.QueueCap,
            queueCounts.GetValueOrDefault(w.Id), matchCounts.GetValueOrDefault(w.Id)
        )).ToList();
    }

    public async Task<OpenPlayWindowState> GetStateAsync(Guid windowId, CancellationToken ct = default)
    {
        var window = await _db.RoomStatusWindows
            .Include(w => w.Room).ThenInclude(r => r.Game)
            .FirstOrDefaultAsync(w => w.Id == windowId, ct)
            ?? throw new KeyNotFoundException("Window not found.");

        var queue = await _db.QueueEntries
            .Where(q => q.WindowId == windowId && q.State == QueueState.Queued)
            .Include(q => q.Party).ThenInclude(p => p.Leader)
            .Include(q => q.Party).ThenInclude(p => p.Partner)
            .OrderBy(q => q.EnqueuedAt)
            .ToListAsync(ct);

        var queueDtos = queue.Select(q => new QueuePartyDto(
            q.PartyId, q.Party.Size,
            q.Party.LeaderUserId, FullName(q.Party.Leader),
            q.Party.PartnerUserId, q.Party.Partner is null ? null : FullName(q.Party.Partner),
            q.EnqueuedAt, q.State
        )).ToList();

        var matches = await _db.Matches
            .Where(m => m.WindowId == windowId && m.EndedAt == null)
            .Include(m => m.Players).ThenInclude(p => p.User)
            .OrderBy(m => m.StartedAt)
            .ToListAsync(ct);

        var matchDtos = matches.Select(m => new MatchDto(
            m.Id, m.WindowId, m.RoomId, m.StartedAt, m.EndedAt,
            m.Players.Select(p => new MatchPlayerDto(p.UserId, FullName(p.User), p.PartyId)).ToList()
        )).ToList();

        var queueLength = queue.Sum(q => q.Party.Size);

        var summary = new OpenPlayWindowSummary(
            window.Id, window.RoomId, window.Room.Name, window.Room.GameId, window.Room.Game.Name,
            window.StartTime, window.EndTime, window.SeatRate ?? 0, window.MatchSize ?? 0, window.QueueCap,
            queueLength, matches.Count
        );

        return new OpenPlayWindowState(summary, queueDtos, matchDtos);
    }

    private async Task BroadcastWindowAsync(Guid windowId, CancellationToken ct)
    {
        var state = await GetStateAsync(windowId, ct);
        await _broadcaster.BroadcastWindowAsync(windowId, state, ct);
    }

    private async Task<MatchDto> BuildMatchDtoAsync(Guid matchId, CancellationToken ct)
    {
        var m = await _db.Matches
            .Include(x => x.Players).ThenInclude(p => p.User)
            .FirstAsync(x => x.Id == matchId, ct);
        return new MatchDto(m.Id, m.WindowId, m.RoomId, m.StartedAt, m.EndedAt,
            m.Players.Select(p => new MatchPlayerDto(p.UserId, FullName(p.User), p.PartyId)).ToList());
    }

    private static string FullName(User u) => $"{u.FirstName} {u.LastName}".Trim();
}
