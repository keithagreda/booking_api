namespace booking_api.Models;

public class Room : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
