namespace booking_api.Models;

public class MatchPlayer : BaseEntity
{
    public Guid MatchId { get; set; }
    public Match Match { get; set; } = null!;

    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? PartyId { get; set; }
    public Party? Party { get; set; }
}
