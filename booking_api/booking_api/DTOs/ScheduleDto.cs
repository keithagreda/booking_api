using booking_api.Models;

namespace booking_api.DTOs;

public record ScheduleGameDto(
    Guid Id,
    string Name
);

public record ScheduleRoomDto(
    Guid Id,
    string Name,
    GameDto Game
);

public record ScheduleBookingDto(
    Guid Id,
    Guid RoomId,
    string BookerName,
    BookingType Type,
    BookingStatus Status,
    DateTime StartTime,
    DateTime EndTime,
    decimal TotalAmount
);

public record ScheduleOpenPlayDto(
    Guid WindowId,
    Guid RoomId,
    RoomStatus Status,
    string WindowNotes,
    int? MatchSize,
    DateTime StartTime,
    DateTime EndTime
);

public record ScheduleDayDto(
    List<ScheduleRoomDto> Rooms,
    List<ScheduleBookingDto> Bookings,
    List<ScheduleOpenPlayDto> OpenPlayWindows
);

public record AdminBookingSummaryDto(
    Guid Id,
    Guid RoomId,
    string BookerName,
    string RoomName,
    BookingType Type,
    BookingStatus Status,
    DateTime StartTime,
    DateTime EndTime,
    decimal TotalAmount
);

public record UpcomingEventDto(
    Guid Id,
    string RoomName,
    string GameName,
    RoomStatus Status,
    DateTime StartTime,
    DateTime EndTime,
    string? Notes,
    int? MatchSize,
    decimal? SeatRate
);
