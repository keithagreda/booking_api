using booking_api.Models;

namespace booking_api.DTOs;

public record RoomSlotDto(
    DateTime Start,
    DateTime End,
    RoomStatus Status,
    bool Available,
    Guid? WindowId,
    decimal? SeatRate,
    int? MatchSize,
    int? QueueLength
);

public record RoomAvailabilityDto(RoomDto Room, IReadOnlyList<RoomSlotDto> Slots);

public record AvailabilityResponse(
    GameDto Game,
    DateTime From,
    DateTime To,
    IReadOnlyList<RoomAvailabilityDto> Rooms
);
