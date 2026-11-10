using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class ListPlatformInvoicesHandler(ISubscriptionInvoiceRepository invoices, IOrganizationRepository organizations, IClock clock)
    : IQueryHandler<ListPlatformInvoicesQuery, PagedResult<PlatformInvoiceRow>>
{
    public async Task<PagedResult<PlatformInvoiceRow>> Handle(ListPlatformInvoicesQuery query, CancellationToken ct)
    {
        // 1. The invoices in the chosen state, with the client names to search and show.
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var names = await organizations.NamesByIdAsync(ct);
        IEnumerable<SubscriptionInvoice> rows = await invoices.ListForConsoleAsync(query.OrgId, query.Status, today, ct);

        // 2. Search by number or client name, then page.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            rows = rows.Where(i => i.Number.Contains(s, StringComparison.OrdinalIgnoreCase) || names.GetValueOrDefault(i.OrgId, "").Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        var list = rows.ToList();
        return new(list.Skip((query.PageNumber - 1) * query.Size).Take(query.Size).Select(i => new PlatformInvoiceRow(
            i.Id, i.Number, i.Status, i.OrgId, names.GetValueOrDefault(i.OrgId, "Client"), i.Description, i.Total, i.Currency, i.IssuedOn, i.DueOn, i.PaidOn, i.PaymentMethod, i.PaymentReference,
            i.Status == SubscriptionInvoiceStatus.Open && i.DueOn < today)).ToList(), list.Count, query.PageNumber, query.Size);
    }
}
