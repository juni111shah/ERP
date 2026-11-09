using ERP.Application.Abstractions;
using ERP.Modules.Services.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Services.Repositories;

internal sealed class RetainerBillingPeriodRepository(IRepository<RetainerBillingPeriod> inner) : RepositoryBase<RetainerBillingPeriod>(inner), IRetainerBillingPeriodRepository
{
    public Task<RetainerBillingPeriod?> FindForPeriodAsync(Guid retainerId, DateOnly periodStart, CancellationToken ct)
        => Query(new Specification<RetainerBillingPeriod>(x => x.RetainerId == retainerId && x.PeriodStart == periodStart)).FirstOrDefaultAsync(ct);

    public Task<List<BilledPeriod>> ListBilledPeriodsAsync(Guid retainerId, CancellationToken ct)
        => Query(new Specification<RetainerBillingPeriod>(x => x.RetainerId == retainerId)).Select(x => new BilledPeriod(x.PeriodStart, x.PeriodEnd)).ToListAsync(ct);

    public Task<List<RetainerBillingPeriod>> ListOnInvoiceForUpdateAsync(Guid invoiceId, CancellationToken ct)
        => Tracked(new Specification<RetainerBillingPeriod>(x => x.InvoiceId == invoiceId)).ToListAsync(ct);
}
