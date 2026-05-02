using booking_api.Models;

namespace booking_api.DTOs;

public record DisplayRoomState(
    Guid RoomId,
    string RoomName,
    Guid GameId,
    string GameName,
    RoomStatus CurrentStatus,
    string? CurrentReservationLabel,
    DateTime? CurrentEndsAt,
    DateTime? NextStartsAt,
    string? NextLabel,
    int? OpenPlayQueueLength,
    int? OpenPlayActiveMatches,
    Guid? CurrentMatchId,
    IReadOnlyList<MatchPlayerDto>? CurrentMatchPlayers
);

public record DisplaySnapshot(DateTime AsOf, IReadOnlyList<DisplayRoomState> Rooms);
