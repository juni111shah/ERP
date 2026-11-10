using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Assistant.Domain;
using ERP.Modules.Assistant.Repositories;

namespace ERP.Modules.Assistant.Features;

internal sealed class CancelAssistantActionHandler(IAssistantPendingActionRepository actions, ICurrentUser user) : ICommandHandler<CancelAssistantActionCommand, AssistantActionCard>
{
    private Guid Me => user.UserId ?? throw new ForbiddenException();

    public async Task<AssistantActionCard> Handle(CancelAssistantActionCommand cmd, CancellationToken ct)
    {
        var row = await actions.GetOwnForUpdateAsync(cmd.Id, Me, ct);
        if (row.Status == AssistantPendingAction.Pending)
        {
            row.Status = AssistantPendingAction.Cancelled;
            await actions.SaveChangesAsync(ct);
        }
        return AssistantActionCard.From(row);
    }
}
