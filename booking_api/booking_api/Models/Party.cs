namespace booking_api.Models;

public enum PartyState
{
    PendingPartner,
    Confirmed,
    Cancelled
}

public class Party : BaseEntity
{
    public Guid WindowId { get; set; }
    public RoomStatusWindow Window { get; set; } = null!;

    public Guid LeaderUserId { get; set; }
    public User Leader { get; set; } = null!;

    public Guid? PartnerUserId { get; set; }
    public User? Partner { get; set; }

    public int Size { get; set; } = 1;
    public PartyState State { get; set; } = PartyState.Confirmed;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
