using ERP.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ERP.Api.Infrastructure;

/// <summary>Real-time notifications. Each connection joins its own user group only; clients cannot join other groups.</summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var sub = Context.User?.FindFirst("sub")?.Value;
        if (sub is not null) await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{sub}");
        await base.OnConnectedAsync();
    }
}

public sealed class SignalRNotificationPusher(IHubContext<NotificationsHub> hub) : INotificationPusher
{
    public Task PushAsync(Guid userId, object payload, CancellationToken ct = default)
        => hub.Clients.Group($"user:{userId}").SendAsync("notification", payload, ct);
}
