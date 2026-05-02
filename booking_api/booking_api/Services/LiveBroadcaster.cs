using booking_api.DTOs;
using booking_api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace booking_api.Services;

public class LiveBroadcaster : ILiveBroadcaster
{
    private readonly IHubContext<LiveHub> _hub;

    public LiveBroadcaster(IHubContext<LiveHub> hub) => _hub = hub;

    public Task BroadcastWindowAsync(Guid windowId, OpenPlayWindowState state, CancellationToken ct = default)
        => _hub.Clients.Group(LiveHub.GroupForWindow(windowId)).SendAsync("WindowState", state, ct);

    public Task BroadcastDisplayAsync(DisplaySnapshot snapshot, CancellationToken ct = default)
        => _hub.Clients.Group(LiveHub.DisplayGroup).SendAsync("DisplayState", snapshot, ct);
}
