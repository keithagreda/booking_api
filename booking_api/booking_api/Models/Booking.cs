namespace booking_api.Models;

public class Booking : BaseEntity
{
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public Guid BookedByUserId { get; set; }
    public User? BookedByUser { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Notes { get; set; }

    public BookingType Type { get; set; } = BookingType.Regular;
    public BookingStatus Status { get; set; } = BookingStatus.PendingPayment;

    public decimal TotalAmount { get; set; }
    public DateTime? HoldExpiresAt { get; set; }

    // OpenPlaySeat-only links.
    public Guid? PartyId { get; set; }
    public Party? Party { get; set; }

    public Guid? WindowId { get; set; }
    public RoomStatusWindow? Window { get; set; }

    public Payment? Payment { get; set; }
}
