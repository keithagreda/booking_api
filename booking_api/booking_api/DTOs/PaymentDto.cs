using booking_api.Models;

namespace booking_api.DTOs;

public record PaymentDto(
    Guid Id,
    Guid BookingId,
    string? BookerName,
    string? BookerEmail,
    string? RoomName,
    DateTime? BookingStartTime,
    DateTime? BookingEndTime,
    PaymentMethod Method,
    PaymentStatus Status,
    decimal Amount,
    string? ReferenceNumber,
    string? ProofS3Key,
    string? ProofPresignedUrl,
    string? Remarks,
    string? RejectionReason,
    DateTime? ReviewedAt
);

public record SubmitProofRequest(string? ReferenceNumber);

public record RejectPaymentRequest(string Reason);

public record SettlePaymentRequest(PaymentMethod Method, string? ReferenceNumber, string? Remarks);
