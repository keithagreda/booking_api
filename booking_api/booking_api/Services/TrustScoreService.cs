using booking_api.Data;
using booking_api.Models;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Services;

public interface ITrustScoreService
{
    Task AdjustAsync(Guid userId, TrustAdjustmentReason reason, float adjustment, string? details = null, Guid? bookingId = null, Guid? triggeredByUserId = null, CancellationToken ct = default);
    Task ApplyNaturalRecoveryAsync(CancellationToken ct = default);
}

public class TrustScoreService : ITrustScoreService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TrustScoreService> _log;

    public TrustScoreService(AppDbContext db, ILogger<TrustScoreService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task AdjustAsync(Guid userId, TrustAdjustmentReason reason, float adjustment, string? details = null, Guid? bookingId = null, Guid? triggeredByUserId = null, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], cancellationToken: ct);
        if (user == null) return;

        var previous = user.TrustScore;
        var newScore = Math.Clamp(previous + adjustment, 0f, 100f);
        user.TrustScore = newScore;
        user.LastTrustAdjustment = DateTime.UtcNow;

        var entry = new TrustScoreHistory
        {
            UserId = userId,
            PreviousScore = previous,
            NewScore = newScore,
            Adjustment = adjustment,
            Reason = reason,
            Details = details,
            BookingId = bookingId,
            TriggeredByUserId = triggeredByUserId,
            CreationTime = DateTime.UtcNow
        };

        _db.TrustScoreHistory.Add(entry);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "Trust score for user {UserId} adjusted: {Previous} → {New} ({Reason}, {Adjustment})",
            userId, previous, newScore, reason, adjustment > 0 ? $"+{adjustment}" : adjustment.ToString());
    }

    public async Task ApplyNaturalRecoveryAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var users = await _db.Users
            .Where(u => u.TrustScore < 100f && u.LastTrustAdjustment != null && u.LastTrustAdjustment < cutoff)
            .ToListAsync(ct);

        int adjusted = 0;
        foreach (var user in users)
        {
            var hoursSinceLast = (DateTime.UtcNow - user.LastTrustAdjustment!.Value).TotalHours;
            var recovery = (float)Math.Min(hoursSinceLast * 0.01, 5f);

            if (recovery < 0.01f) continue;

            var previous = user.TrustScore;
            user.TrustScore = Math.Min(previous + recovery, 100f);
            user.LastTrustAdjustment = DateTime.UtcNow;

            _db.TrustScoreHistory.Add(new TrustScoreHistory
            {
                UserId = user.Id,
                PreviousScore = previous,
                NewScore = user.TrustScore,
                Adjustment = recovery,
                Reason = TrustAdjustmentReason.NaturalRecovery,
                Details = $"Auto-recovery after {hoursSinceLast:F1} hours",
                CreationTime = DateTime.UtcNow
            });

            adjusted++;
        }

        if (adjusted > 0)
        {
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Applied natural recovery to {Count} users", adjusted);
        }
    }
}
