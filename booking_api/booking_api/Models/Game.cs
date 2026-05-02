namespace booking_api.Models;

public class Game : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}
