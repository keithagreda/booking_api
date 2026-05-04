using Microsoft.AspNetCore.Identity;

namespace booking_api.Models;

public class User : IdentityUser<Guid>
{
    public User()
    {
        Id = Guid.CreateVersion7();
    }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.Player;
    public bool IsBanned { get; set; }
    public DateTime? BannedAt { get; set; }
    public Guid? BannedByUserId { get; set; }

    // Trust / behavior score (0-100, default 50)
    public float TrustScore { get; set; } = 50f;
    public DateTime? LastTrustAdjustment { get; set; }

    // Outstanding balance (unpaid bookings + POS tabs)
    public decimal OutstandingBalance { get; set; }

    // Provisional flag — admin-created placeholder until user self-registers
    public bool IsProvisional { get; set; }

    // Audit fields (mirroring BaseEntity for non-Identity entities)
    public Guid? CreatorUserId { get; set; }
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
    public Guid? LastModifiedByUserId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public DateTime? DeletionTime { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<TrustScoreHistory> TrustScoreHistory { get; set; } = new List<TrustScoreHistory>();
}
