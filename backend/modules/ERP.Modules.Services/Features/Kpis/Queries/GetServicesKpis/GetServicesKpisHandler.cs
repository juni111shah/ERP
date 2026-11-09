using ERP.Application.Common;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Repositories;

namespace ERP.Modules.Services.Features;

internal sealed class GetServicesKpisHandler(
    IServiceEngagementRepository engagements,
    IRetainerContractRepository retainers,
    IBillingMilestoneRepository milestones,
    ITimeEntryRecordRepository timeEntries)
    : IQueryHandler<GetServicesKpisQuery, ServicesKpisDto>
{
    public async Task<ServicesKpisDto> Handle(GetServicesKpisQuery request, CancellationToken cancellationToken)
    {
        // 1. Active engagements and their contract value.
        var activeEngagements = await engagements.CountActiveAsync(request.LegalEntityId, cancellationToken);
        var totalContractValue = await engagements.TotalActiveContractValueAsync(request.LegalEntityId, cancellationToken);

        // 2. Monthly recurring revenue of the active retainers.
        var activeRetainers = await retainers.ListActiveAsync(request.LegalEntityId, cancellationToken);
        var mrr = activeRetainers.Sum(r => r.Cycle switch
        {
            RetainerCycle.Monthly => r.RecurringFee,
            RetainerCycle.Quarterly => r.RecurringFee / 3m,
            RetainerCycle.Annual => r.RecurringFee / 12m,
            _ => r.RecurringFee
        });

        // 3. The milestone pipeline.
        var pendingMls = await milestones.TotalInStatusAsync(request.LegalEntityId, MilestoneStatus.Planned, cancellationToken);
        var readyMls = await milestones.TotalInStatusAsync(request.LegalEntityId, MilestoneStatus.ReadyToInvoice, cancellationToken);

        // 4. Hours and utilisation.
        var totalHours = await timeEntries.TotalHoursAsync(request.LegalEntityId, billableOnly: false, cancellationToken);
        var billableHours = await timeEntries.TotalHoursAsync(request.LegalEntityId, billableOnly: true, cancellationToken);

        var utilization = totalHours > 0 ? Math.Round((billableHours / totalHours) * 100m, 1) : 0m;

        return new ServicesKpisDto(
            activeEngagements,
            totalContractValue,
            Math.Round(mrr, 2),
            pendingMls,
            readyMls,
            totalHours,
            billableHours,
            utilization);
    }
}
