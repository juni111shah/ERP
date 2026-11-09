using ERP.Application.Abstractions;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Workflow;

internal sealed class MarkAllNotificationsReadHandler(INotificationRepository notifications, ICurrentUser user, IClock clock) : ICommandHandler<MarkAllNotificationsReadCommand>
{
    public async Task<Unit> Handle(MarkAllNotificationsReadCommand command, CancellationToken ct)
    {
        var now = clock.UtcNow;
        foreach (var n in await notifications.ListUnreadForUserForUpdateAsync(user.UserId, ct)) n.ReadAt = now;
        await notifications.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
