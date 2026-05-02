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
    public DbSet<Game> Games => Set<Game>();
    public DbSet<RoomStatusWindow> RoomStatusWindows => Set<RoomStatusWindow>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Role).HasConversion<string>();
            entity.HasQueryFilter(u => !u.IsDeleted);
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasOne(r => r.Game)
                .WithMany(g => g.Rooms)
                .HasForeignKey(r => r.GameId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(r => r.HourlyRate).HasPrecision(18, 2);
        });

        modelBuilder.Entity<RoomStatusWindow>(entity =>
        {
            entity.HasOne(w => w.Room)
                .WithMany(r => r.StatusWindows)
                .HasForeignKey(w => w.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(w => w.Status).HasConversion<string>();
            entity.Property(w => w.SeatRate).HasPrecision(18, 2);
            entity.HasIndex(w => new { w.RoomId, w.StartTime, w.EndTime });
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasOne(b => b.BookedByUser)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.BookedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Room)
                .WithMany(r => r.Bookings)
                .HasForeignKey(b => b.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Party)
                .WithMany(p => p.Bookings)
                .HasForeignKey(b => b.PartyId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(b => b.Window)
                .WithMany()
                .HasForeignKey(b => b.WindowId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(b => b.Type).HasConversion<string>();
            entity.Property(b => b.Status).HasConversion<string>();
            entity.Property(b => b.TotalAmount).HasPrecision(18, 2);

            entity.HasIndex(b => new { b.RoomId, b.StartTime, b.EndTime });
            entity.HasIndex(b => b.Status);
        });

        modelBuilder.Entity<Party>(entity =>
        {
            entity.HasOne(p => p.Window)
                .WithMany()
                .HasForeignKey(p => p.WindowId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.Leader)
                .WithMany()
                .HasForeignKey(p => p.LeaderUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Partner)
                .WithMany()
                .HasForeignKey(p => p.PartnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(p => p.State).HasConversion<string>();
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasOne(p => p.Booking)
                .WithOne(b => b.Payment)
                .HasForeignKey<Payment>(p => p.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(p => p.Method).HasConversion<string>();
            entity.Property(p => p.Status).HasConversion<string>();
            entity.Property(p => p.Amount).HasPrecision(18, 2);

            entity.HasIndex(p => p.Status);
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasOne(m => m.Window)
                .WithMany()
                .HasForeignKey(m => m.WindowId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Room)
                .WithMany()
                .HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(m => new { m.WindowId, m.EndedAt });
        });

        modelBuilder.Entity<MatchPlayer>(entity =>
        {
            entity.HasOne(mp => mp.Match)
                .WithMany(m => m.Players)
                .HasForeignKey(mp => mp.MatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(mp => mp.Booking)
                .WithMany()
                .HasForeignKey(mp => mp.BookingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(mp => mp.User)
                .WithMany()
                .HasForeignKey(mp => mp.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(mp => mp.Party)
                .WithMany()
                .HasForeignKey(mp => mp.PartyId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<QueueEntry>(entity =>
        {
            entity.HasOne(q => q.Window)
                .WithMany()
                .HasForeignKey(q => q.WindowId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(q => q.Party)
                .WithMany()
                .HasForeignKey(q => q.PartyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(q => q.CurrentMatch)
                .WithMany()
                .HasForeignKey(q => q.CurrentMatchId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(q => q.State).HasConversion<string>();
            entity.HasIndex(q => new { q.WindowId, q.State, q.EnqueuedAt });
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
