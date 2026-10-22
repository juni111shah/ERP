using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Workflow;

public sealed record NotificationDto(Guid Id, string Title, string? Body, string? Link, string Severity, DateTime CreatedAt, DateTime? ReadAt);
public sealed record NotificationList(int Unread, IReadOnlyList<NotificationDto> Items);

/// <summary>The caller's latest notifications and unread count.</summary>
public sealed record ListNotifications : IQuery<NotificationList>;
public sealed record MarkNotificationRead(Guid Id) : ICommand;

internal sealed class NotificationHandlers(IRepository<Notification> notifications, ICurrentUser user, IClock clock) :
    IRequestHandler<ListNotifications, NotificationList>,
    IRequestHandler<MarkNotificationRead, Unit>
{
    public async Task<NotificationList> Handle(ListNotifications q, CancellationToken ct)
    {
        var me = user.UserId;
        var unread = await notifications.CountAsync(x => x.UserId == me && x.ReadAt == null, ct);
        var items = await notifications.Query(new Specification<Notification>(x => x.UserId == me)).OrderByDescending(x => x.CreatedAt).Take(50)
            .Select(x => new NotificationDto(x.Id, x.Title, x.Body, x.Link, x.Severity, x.CreatedAt, x.ReadAt)).ToListAsync(ct);
        return new(unread, items);
    }

    public async Task<Unit> Handle(MarkNotificationRead cmd, CancellationToken ct)
    {
        var me = user.UserId;
        var n = await notifications.Tracked(new Specification<Notification>(x => x.Id == cmd.Id && x.UserId == me)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        n.ReadAt ??= clock.UtcNow;
        await notifications.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
