using ERP.Application.Abstractions;
using ERP.Modules.Services.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Services.Repositories;

internal sealed class TimeEntryRecordRepository(IRepository<TimeEntryRecord> inner) : RepositoryBase<TimeEntryRecord>(inner), ITimeEntryRecordRepository
{
    public Task<TimeEntryRecord?> FindForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<TimeEntryRecord>> ListUninvoicedApprovedForUpdateAsync(Guid engagementId, DateOnly from, DateOnly to, CancellationToken ct)
        => Tracked(new Specification<TimeEntryRecord>(t => t.EngagementId == engagementId && t.IsApproved && t.IsBillable && t.InvoiceId == null && t.ActivityDate >= from && t.ActivityDate <= to)).ToListAsync(ct);

    public Task<List<TimeEntryRecord>> ListReadyToInvoiceForUpdateAsync(Guid engagementId, IReadOnlyList<Guid>? ids, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var query = Tracked(new Specification<TimeEntryRecord>(t => t.EngagementId == engagementId && t.IsApproved && t.IsBillable && t.InvoiceId == null));
        if (ids is { Count: > 0 } only) query = query.Where(t => only.Contains(t.Id));
        if (from is { } start) query = query.Where(t => t.ActivityDate >= start);
        if (to is { } end) query = query.Where(t => t.ActivityDate <= end);
        return query.ToListAsync(ct);
    }

    public async Task<decimal> UnapprovedBillableHoursAsync(Guid engagementId, DateOnly start, DateOnly end, CancellationToken ct)
        => (await Query(new Specification<TimeEntryRecord>(t => t.EngagementId == engagementId && !t.IsApproved && t.IsBillable && t.ActivityDate >= start && t.ActivityDate <= end)).Select(t => t.Hours).ToListAsync(ct)).Sum();

    public Task<List<TimeEntryRecord>> ListOnInvoiceForUpdateAsync(Guid invoiceId, CancellationToken ct)
        => Tracked(new Specification<TimeEntryRecord>(x => x.InvoiceId == invoiceId)).ToListAsync(ct);

    public async Task<decimal> TotalHoursAsync(Guid legalEntityId, bool billableOnly, CancellationToken ct)
    {
        var timeQuery = Query().Where(t => t.LegalEntityId == legalEntityId);
        return billableOnly
            ? await timeQuery.Where(t => t.IsBillable).SumAsync(t => (decimal?)t.Hours, ct) ?? 0m
            : await timeQuery.SumAsync(t => (decimal?)t.Hours, ct) ?? 0m;
    }
}
