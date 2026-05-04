using booking_api.Models;

namespace booking_api.Models;

public class TrustScoreHistory : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public float PreviousScore { get; set; }
    public float NewScore { get; set; }
    public float Adjustment { get; set; }

    public TrustAdjustmentReason Reason { get; set; }
    public string? Details { get; set; }

    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }

    public Guid? TriggeredByUserId { get; set; }
    public User? TriggeredByUser { get; set; }
}

public enum TrustAdjustmentReason
{
    BookingApproved,
    PaymentRejected,
    BookingExpired,
    BookingCancelled,
    NoShow,
    ManualAdjustment,
    NaturalRecovery,
    BookingCompleted
}
