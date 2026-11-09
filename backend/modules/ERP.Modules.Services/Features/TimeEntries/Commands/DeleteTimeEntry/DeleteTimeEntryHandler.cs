using ERP.Application.Common;
using ERP.Modules.Services.Repositories;
using MediatR;

namespace ERP.Modules.Services.Features;

internal sealed class DeleteTimeEntryHandler(ITimeEntryRecordRepository time, IServiceEngagementRepository engagements) : ICommandHandler<DeleteTimeEntryCommand>
{
    public async Task<Unit> Handle(DeleteTimeEntryCommand request, CancellationToken cancellationToken)
    {
        // 1. Only unapproved time can be deleted.
        var entry = TimeEntryRules.Unapproved(await time.FindForUpdateAsync(request.Id, cancellationToken), request.Id, "deleted");

        // 2. Take it off the engagement's consumption.
        var engagement = await engagements.FindForUpdateAsync(entry.EngagementId, cancellationToken);
        if (engagement is not null)
        {
            engagement.ConsumedHours -= entry.Hours;
            engagement.ConsumedCost -= entry.TotalAmount;
        }

        // 3. Delete.
        time.Remove(entry);
        await time.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
