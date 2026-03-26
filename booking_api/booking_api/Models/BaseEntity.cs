namespace booking_api.Models;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // Audit: Creation
    public Guid? CreatorUserId { get; set; }
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;

    // Audit: Modification
    public Guid? LastModifiedByUserId { get; set; }
    public DateTime? LastModificationTime { get; set; }

    // Soft Delete
    public Guid? DeletedByUserId { get; set; }
    public DateTime? DeletionTime { get; set; }
    public bool IsDeleted { get; set; }
}
