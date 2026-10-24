using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Services.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Services.Features;

public record ServicesKpisDto(
    int ActiveEngagements,
    decimal TotalContractValue,
    decimal RetainerMonthlyRecurringRevenue,
    decimal PendingMilestonesValue,
    decimal ReadyToInvoiceMilestonesValue,
    decimal TotalLoggedHours,
    decimal BillableHours,
    decimal BillableUtilizationRate);

public record GetServicesKpisQuery(Guid LegalEntityId) : IQuery<ServicesKpisDto>;

public class ServicesKpiHandlers : IRequestHandler<GetServicesKpisQuery, ServicesKpisDto>
{
    private readonly IReadRepository<ServiceEngagement> _engagements;
    private readonly IReadRepository<RetainerContract> _retainers;
    private readonly IReadRepository<BillingMilestone> _milestones;
    private readonly IReadRepository<TimeEntryRecord> _timeEntries;

    public ServicesKpiHandlers(
        IReadRepository<ServiceEngagement> engagements,
        IReadRepository<RetainerContract> retainers,
        IReadRepository<BillingMilestone> milestones,
        IReadRepository<TimeEntryRecord> timeEntries)
    {
        _engagements = engagements;
        _retainers = retainers;
        _milestones = milestones;
        _timeEntries = timeEntries;
    }

    public async Task<ServicesKpisDto> Handle(GetServicesKpisQuery request, CancellationToken cancellationToken)
    {
        var engQuery = _engagements.Query().Where(e => e.LegalEntityId == request.LegalEntityId);
        var activeEngagements = await engQuery.CountAsync(e => e.Status == EngagementStatus.Active, cancellationToken);
        var totalContractValue = await engQuery.Where(e => e.Status == EngagementStatus.Active).SumAsync(e => (decimal?)e.ContractValue, cancellationToken) ?? 0m;

        var retQuery = _retainers.Query().Where(r => r.LegalEntityId == request.LegalEntityId && r.Status == RetainerStatus.Active);
        var activeRetainers = await retQuery.ToListAsync(cancellationToken);
        var mrr = activeRetainers.Sum(r => r.Cycle switch
        {
            RetainerCycle.Monthly => r.RecurringFee,
            RetainerCycle.Quarterly => r.RecurringFee / 3m,
            RetainerCycle.Annual => r.RecurringFee / 12m,
            _ => r.RecurringFee
        });

        var mlsQuery = _milestones.Query().Where(m => m.LegalEntityId == request.LegalEntityId);
        var pendingMls = await mlsQuery.Where(m => m.Status == MilestoneStatus.Planned).SumAsync(m => (decimal?)m.Amount, cancellationToken) ?? 0m;
        var readyMls = await mlsQuery.Where(m => m.Status == MilestoneStatus.ReadyToInvoice).SumAsync(m => (decimal?)m.Amount, cancellationToken) ?? 0m;

        var timeQuery = _timeEntries.Query().Where(t => t.LegalEntityId == request.LegalEntityId);
        var totalHours = await timeQuery.SumAsync(t => (decimal?)t.Hours, cancellationToken) ?? 0m;
        var billableHours = await timeQuery.Where(t => t.IsBillable).SumAsync(t => (decimal?)t.Hours, cancellationToken) ?? 0m;

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
