using ERP.Application.Abstractions;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Workflow;

internal sealed class MarkNotificationReadHandler(INotificationRepository notifications, ICurrentUser user, IClock clock) : ICommandHandler<MarkNotificationReadCommand>
{
    public async Task<Unit> Handle(MarkNotificationReadCommand command, CancellationToken ct)
    {
        var n = await notifications.GetForUserForUpdateAsync(command.Id, user.UserId, ct);
        n.ReadAt ??= clock.UtcNow;
        await notifications.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
