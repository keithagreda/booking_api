namespace booking_api.Models;

public enum PaymentMethod
{
    GCash,
    Cash,
    OnlineBanking
}

public enum PaymentStatus
{
    AwaitingProof,
    Submitted,
    Approved,
    Rejected,
    Outstanding
}

public class Payment : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public PaymentMethod Method { get; set; } = PaymentMethod.GCash;
    public PaymentStatus Status { get; set; } = PaymentStatus.AwaitingProof;
    public decimal Amount { get; set; }

    public string? ReferenceNumber { get; set; }
    public string? ProofS3Key { get; set; }
    public string? Remarks { get; set; }

    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}
