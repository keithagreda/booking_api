using booking_api.Models;

namespace booking_api.DTOs;

public record PaymentDto(
    Guid Id,
    Guid BookingId,
    PaymentMethod Method,
    PaymentStatus Status,
    decimal Amount,
    string? GcashReference,
    string? ProofS3Key,
    string? ProofPresignedUrl,
    string? RejectionReason,
    DateTime? ReviewedAt
);

public record SubmitProofRequest(string? GcashReference);

public record RejectPaymentRequest(string Reason);
