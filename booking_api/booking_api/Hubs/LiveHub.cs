using Microsoft.AspNetCore.SignalR;

namespace booking_api.Hubs;

public class LiveHub : Hub
{
    public Task SubscribeWindow(Guid windowId)
        => Groups.AddToGroupAsync(Context.ConnectionId, GroupForWindow(windowId));

    public Task UnsubscribeWindow(Guid windowId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupForWindow(windowId));

    public Task SubscribeDisplay()
        => Groups.AddToGroupAsync(Context.ConnectionId, DisplayGroup);

    public Task UnsubscribeDisplay()
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, DisplayGroup);

    public static string GroupForWindow(Guid windowId) => $"window:{windowId}";
    public const string DisplayGroup = "display:venue";
}
