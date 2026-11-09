using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Raises a work order for every active schedule whose lead time has arrived, then moves it to its next occurrence. Idempotent per day.</summary>
internal sealed class PmGenerator(IPmScheduleRepository schedules, IRepository<WorkOrder> workOrders, INumberingService numbers, IUnitOfWork unitOfWork)
{
    public Task<int> RunAsync(DateOnly today, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        var due = await schedules.ListActiveForUpdateAsync(token);
        var created = 0;
        foreach (var s in due.Where(s => s.NextDue.AddDays(-s.LeadDays) <= today))
        {
            var w = new WorkOrder
            {
                LegalEntityId = s.LegalEntityId, PropertyId = s.PropertyId, UnitId = s.UnitId, Issue = s.Title, Category = s.Category, Priority = s.Priority,
                ReportedOn = today, ReportedBy = "Preventive maintenance", ScheduledFor = s.NextDue, EstimatedCost = s.EstimatedCost, Details = s.Notes,
            };
            w.Number = await numbers.NextAsync(Docs.WorkOrder, w.LegalEntityId, today, token);
            workOrders.Add(w);
            created++;
            s.LastGeneratedOn = today;
            var next = s.NextDue.AddMonths(s.FrequencyMonths);
            while (next.AddDays(-s.LeadDays) <= today) next = next.AddMonths(s.FrequencyMonths); // skip occurrences already missed
            s.NextDue = next;
        }
        await schedules.SaveChangesAsync(token);
        return created;
    }, ct);
}

internal sealed class PmJob(PmGenerator generator) : IScheduledJob
{
    public string Name => "realestate.preventive-maintenance";
    public string? ModuleKey => RealEstateModule.Key;
    public Task RunAsync(DateOnly today, CancellationToken ct) => generator.RunAsync(today, ct);
}
