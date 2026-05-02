using booking_api.Models;

namespace booking_api.DTOs;

public record OpenPlayWindowSummary(
    Guid WindowId,
    Guid RoomId,
    string RoomName,
    Guid GameId,
    string GameName,
    DateTime StartTime,
    DateTime EndTime,
    decimal SeatRate,
    int MatchSize,
    int? QueueCap,
    int QueueLength,
    int ActiveMatchCount
);

public record QueuePartyDto(
    Guid PartyId,
    int Size,
    Guid LeaderUserId,
    string LeaderName,
    Guid? PartnerUserId,
    string? PartnerName,
    DateTime EnqueuedAt,
    QueueState State
);

public record MatchPlayerDto(Guid UserId, string Name, Guid? PartyId);

public record MatchDto(
    Guid Id,
    Guid WindowId,
    Guid RoomId,
    DateTime StartedAt,
    DateTime? EndedAt,
    IReadOnlyList<MatchPlayerDto> Players
);

public record OpenPlayWindowState(
    OpenPlayWindowSummary Summary,
    IReadOnlyList<QueuePartyDto> Queue,
    IReadOnlyList<MatchDto> ActiveMatches
);

public record JoinOpenPlayRequest(Guid? PartnerUserId);

public record JoinOpenPlayResponse(Guid PartyId, IReadOnlyList<BookingDto> Bookings);
