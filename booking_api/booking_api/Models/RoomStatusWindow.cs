namespace booking_api.Models;

public class RoomStatusWindow : BaseEntity
{
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public RoomStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Notes { get; set; }

    // OpenPlay-only configuration. Null for other statuses.
    public decimal? SeatRate { get; set; }
    public int? MatchSize { get; set; }
    public int? QueueCap { get; set; }
}
