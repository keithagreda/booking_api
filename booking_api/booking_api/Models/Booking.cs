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
}
