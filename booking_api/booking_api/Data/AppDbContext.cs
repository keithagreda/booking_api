using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using booking_api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Data;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor? httpContextAccessor = null)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Room> Rooms => Set<Room>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Role).HasConversion<string>();
            entity.HasQueryFilter(u => !u.IsDeleted);
        });

        // Booking → User FK
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasOne(b => b.BookedByUser)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.BookedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Apply global soft-delete filter to all entities inheriting BaseEntity
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var method = typeof(AppDbContext)
                .GetMethod(nameof(ApplySoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(null, [modelBuilder]);
        }
    }

    private static void ApplySoftDeleteFilter<T>(ModelBuilder modelBuilder) where T : BaseEntity
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override int SaveChanges()
    {
        SetAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetAuditFields()
    {
        var now = DateTime.UtcNow;
        var userId = GetCurrentUserId();

        // Audit BaseEntity descendants
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreationTime = now;
                    entry.Entity.CreatorUserId ??= userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.LastModificationTime = now;
                    entry.Entity.LastModifiedByUserId = userId;
                    break;
            }
        }

        // Audit User entities (extends IdentityUser, not BaseEntity)
        foreach (var entry in ChangeTracker.Entries<User>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreationTime = now;
                    entry.Entity.CreatorUserId ??= userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.LastModificationTime = now;
                    entry.Entity.LastModifiedByUserId = userId;
                    break;
            }
        }
    }

    private Guid? GetCurrentUserId()
    {
        var sub = _httpContextAccessor?.HttpContext?.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? _httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        return sub is not null && Guid.TryParse(sub, out var id) ? id : null;
    }
}
