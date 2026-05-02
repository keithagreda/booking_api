namespace booking_api.Models;

public enum QueueState
{
    Queued,
    InMatch,
    Left
}

public class QueueEntry : BaseEntity
{
    public Guid WindowId { get; set; }
    public RoomStatusWindow Window { get; set; } = null!;

    public Guid PartyId { get; set; }
    public Party Party { get; set; } = null!;

    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public QueueState State { get; set; } = QueueState.Queued;

    public Guid? CurrentMatchId { get; set; }
    public Match? CurrentMatch { get; set; }
}
