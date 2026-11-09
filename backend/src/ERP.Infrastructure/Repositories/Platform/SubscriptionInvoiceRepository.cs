using ERP.Application.Abstractions;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Platform;

internal sealed class SubscriptionInvoiceRepository(IRepository<SubscriptionInvoice> invoices) : RepositoryBase<SubscriptionInvoice>(invoices), ISubscriptionInvoiceRepository
{
    public Task<List<SubscriptionInvoice>> ListForClientAsync(Guid orgId, CancellationToken ct)
        => Query().Where(i => i.OrgId == orgId).OrderByDescending(i => i.IssuedOn).ThenByDescending(i => i.Number).ToListAsync(ct);

    public Task<SubscriptionInvoice?> FindForClientAsync(Guid id, Guid orgId, CancellationToken ct) => Query().FirstOrDefaultAsync(i => i.Id == id && i.OrgId == orgId, ct);

    public async Task<Dictionary<Guid, decimal>> OpenBalanceByClientAsync(CancellationToken ct)
        => (await Query().Where(i => i.Status == SubscriptionInvoiceStatus.Open).Select(i => new { i.OrgId, i.Total }).ToListAsync(ct))
            .GroupBy(i => i.OrgId).ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

    public Task<List<SubscriptionInvoiceAmount>> ListAmountsNotVoidAsync(CancellationToken ct)
        => Query().Where(i => i.Status != SubscriptionInvoiceStatus.Void).Select(i => new SubscriptionInvoiceAmount(i.Status, i.Total, i.DueOn, i.PaidOn)).ToListAsync(ct);

    public Task<bool> HasOverdueAsync(Guid orgId, Guid? except, DateOnly today, CancellationToken ct)
        => AnyAsync(i => i.OrgId == orgId && i.Status == SubscriptionInvoiceStatus.Open && i.DueOn < today && i.Id != except, ct);

    public Task<List<SubscriptionInvoice>> ListForConsoleAsync(Guid? orgId, string? status, DateOnly today, CancellationToken ct)
    {
        var query = Query();
        if (orgId is { } id) query = query.Where(i => i.OrgId == id);
        if (status is { Length: > 0 })
        {
            if (status.Equals("overdue", StringComparison.OrdinalIgnoreCase)) query = query.Where(i => i.Status == SubscriptionInvoiceStatus.Open && i.DueOn < today);
            else if (Enum.TryParse<SubscriptionInvoiceStatus>(status, true, out var parsed)) query = query.Where(i => i.Status == parsed);
        }
        return query.OrderByDescending(i => i.IssuedOn).ThenByDescending(i => i.Number).ToListAsync(ct);
    }
}
