using booking_api.Data;
using booking_api.Models;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Services;

public class HoldExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HoldExpiryWorker> _log;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public HoldExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<HoldExpiryWorker> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "HoldExpiryWorker sweep failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { return; }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        var stale = await db.Bookings
            .Where(b => b.Status == BookingStatus.PendingPayment
                && b.HoldExpiresAt != null && b.HoldExpiresAt < now)
            .ToListAsync(ct);

        if (stale.Count == 0)
            return;

        foreach (var b in stale)
            b.Status = BookingStatus.Expired;

        await db.SaveChangesAsync(ct);
        _log.LogInformation("Expired {Count} stale booking holds", stale.Count);
    }
}
