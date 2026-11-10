using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Workflow;

internal sealed class ListNotificationsHandler(INotificationRepository notifications, ICurrentUser user) : IQueryHandler<ListNotificationsQuery, NotificationList>
{
    public async Task<NotificationList> Handle(ListNotificationsQuery query, CancellationToken ct)
    {
        var me = user.UserId;
        var unread = await notifications.CountAsync(x => x.UserId == me && x.ReadAt == null, ct);
        var items = await notifications.ListLatestForUserAsync(me, 50, ct);
        return new(unread, items);
    }
}
