namespace booking_api.Models;

public class Room : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Capacity { get; set; }

    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;

    public decimal HourlyRate { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<RoomStatusWindow> StatusWindows { get; set; } = new List<RoomStatusWindow>();
}
