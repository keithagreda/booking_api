using booking_api.Models;

namespace booking_api.DTOs;

public record CreateGameRequest(string Name, string? Description, string? IconUrl);
public record UpdateGameRequest(string Name, string? Description, string? IconUrl);

public record CreateRoomRequest(Guid GameId, string Name, string? Description, int Capacity, decimal HourlyRate);
public record UpdateRoomRequest(string Name, string? Description, int Capacity, decimal HourlyRate);

public record ScheduleWindowDto(
    Guid Id,
    Guid RoomId,
    RoomStatus Status,
    DateTime StartTime,
    DateTime EndTime,
    string? Notes,
    decimal? SeatRate,
    int? MatchSize,
    int? QueueCap
);

public record CreateScheduleWindowRequest(
    RoomStatus Status,
    DateTime StartTime,
    DateTime EndTime,
    string? Notes,
    decimal? SeatRate,
    int? MatchSize,
    int? QueueCap
);

public record UpdateScheduleWindowRequest(
    RoomStatus Status,
    DateTime StartTime,
    DateTime EndTime,
    string? Notes,
    decimal? SeatRate,
    int? MatchSize,
    int? QueueCap
);
