using booking_api.Models;

namespace booking_api.DTOs;

public record CreateAdminBookingRequest(
    Guid UserId,
    Guid RoomId,
    DateTime StartTime,
    int Hours,
    string? Notes,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber,
    string? Remarks
);

public record UpdateAdminBookingRequest(
    DateTime? StartTime,
    DateTime? EndTime,
    int? Hours,
    string? Notes,
    decimal? TotalAmount
);

public record ProvisionalUserRequest(
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber
);

public record AdminBookingListDto(
    Guid Id,
    Guid RoomId,
    string RoomName,
    string GameName,
    Guid BookedByUserId,
    string BookerName,
    string BookerEmail,
    bool BookerIsProvisional,
    BookingType Type,
    BookingStatus Status,
    DateTime StartTime,
    DateTime EndTime,
    decimal TotalAmount,
    string? Notes,
    DateTime? HoldExpiresAt,
    PaymentSummaryDto? Payment
);

public record PaymentSummaryDto(
    Guid Id,
    PaymentMethod Method,
    PaymentStatus Status,
    decimal Amount,
    string? ReferenceNumber,
    string? Remarks,
    string? ProofPresignedUrl,
    DateTime? ReviewedAt
);
