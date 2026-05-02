using booking_api.DTOs;

namespace booking_api.Services;

public interface ILiveBroadcaster
{
    Task BroadcastWindowAsync(Guid windowId, OpenPlayWindowState state, CancellationToken ct = default);
    Task BroadcastDisplayAsync(DisplaySnapshot snapshot, CancellationToken ct = default);
}
