namespace booking_api.Models;

public class Match : BaseEntity
{
    public Guid WindowId { get; set; }
    public RoomStatusWindow Window { get; set; } = null!;

    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public Guid? EndedByUserId { get; set; }

    public ICollection<MatchPlayer> Players { get; set; } = new List<MatchPlayer>();
}
