using ERP.Application.Common;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Repositories;

namespace ERP.Modules.Services.Features;

internal sealed class UpdateMilestoneStatusHandler(IBillingMilestoneRepository milestones) : ICommandHandler<UpdateMilestoneStatusCommand, MilestoneDto>
{
    public async Task<MilestoneDto> Handle(UpdateMilestoneStatusCommand request, CancellationToken cancellationToken)
    {
        // 1. Load it and check the move is allowed.
        var milestone = await milestones.FindForUpdateAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Billing milestone {request.Id} not found.");

        ServicesTransitions.Milestone.Ensure(milestone.Status, request.Status, isSystem: false);

        // 2. Move it; ready to invoice stamps the completion date once.
        milestone.Status = request.Status;
        if (request.Status == MilestoneStatus.ReadyToInvoice && milestone.CompletionDate is null)
        {
            milestone.CompletionDate = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        }
        if (request.InvoiceId.HasValue)
        {
            milestone.InvoiceId = request.InvoiceId.Value;
        }

        // 3. Save.
        await milestones.SaveChangesAsync(cancellationToken);
        return MilestoneDto.From(milestone);
    }
}
