namespace booking_api.Services;

public interface IMatchmakingService
{
    Task TryFormMatchesAsync(Guid windowId, CancellationToken ct = default);
}
