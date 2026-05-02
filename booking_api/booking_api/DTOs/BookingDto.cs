using booking_api.Models;

namespace booking_api.DTOs;

public record CreateRegularBookingRequest(Guid RoomId, DateTime StartTime, int Hours, string? Notes);

public record BookingDto(
    Guid Id,
    Guid RoomId,
    string RoomName,
    Guid BookedByUserId,
    BookingType Type,
    BookingStatus Status,
    DateTime StartTime,
    DateTime EndTime,
    decimal TotalAmount,
    DateTime? HoldExpiresAt,
    string? Notes,
    PaymentDto? Payment
);
