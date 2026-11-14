using ERP.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ERP.Api.Infrastructure;

/// <summary>
/// Real-time events for signed-in staff: new notifications ("notification") and changes to what the person may do ("access-changed").
/// Each connection joins its own user group only; clients cannot join other groups.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var sub = Context.User?.FindFirst("sub")?.Value;
        if (sub is not null) await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(sub));
        await base.OnConnectedAsync();
    }

    public static string UserGroup(object userId) => $"user:{userId}";
}

public sealed class SignalRNotificationPusher(IHubContext<NotificationsHub> hub) : INotificationPusher
{
    public Task PushAsync(Guid userId, object payload, CancellationToken ct = default) => SendAsync(userId, "notification", payload, ct);

    public Task SendAsync(Guid userId, string eventName, object payload, CancellationToken ct = default)
        => hub.Clients.Group(NotificationsHub.UserGroup(userId)).SendAsync(eventName, payload, ct);
}
