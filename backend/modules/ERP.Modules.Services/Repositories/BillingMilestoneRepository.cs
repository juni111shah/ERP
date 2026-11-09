using ERP.Application.Abstractions;
using ERP.Modules.Services.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Services.Repositories;

internal sealed class BillingMilestoneRepository(IRepository<BillingMilestone> inner) : RepositoryBase<BillingMilestone>(inner), IBillingMilestoneRepository
{
    public Task<BillingMilestone?> FindForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<decimal> TotalForEngagementAsync(Guid engagementId, CancellationToken ct)
        => (await Query(new Specification<BillingMilestone>(x => x.EngagementId == engagementId)).Select(x => x.Amount).ToListAsync(ct)).Sum();

    public Task<List<BillingMilestone>> ListForInvoiceForUpdateAsync(Guid invoiceId, Guid? sourceId, CancellationToken ct)
        => Tracked(new Specification<BillingMilestone>(m => m.InvoiceId == invoiceId || (sourceId.HasValue && m.Id == sourceId.Value))).ToListAsync(ct);

    public async Task<decimal> TotalInStatusAsync(Guid legalEntityId, MilestoneStatus status, CancellationToken ct)
        => await Query().Where(m => m.LegalEntityId == legalEntityId).Where(m => m.Status == status).SumAsync(m => (decimal?)m.Amount, ct) ?? 0m;
}
