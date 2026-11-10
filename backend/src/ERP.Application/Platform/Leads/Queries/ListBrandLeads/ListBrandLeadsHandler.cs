using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class ListBrandLeadsHandler(IBrandLeadRepository leads, ISignupRequestRepository signups) : IQueryHandler<ListBrandLeadsQuery, PagedResult<BrandLeadRow>>
{
    public async Task<PagedResult<BrandLeadRow>> Handle(ListBrandLeadsQuery query, CancellationToken ct)
    {
        BrandLeadStatus? status = Enum.TryParse<BrandLeadStatus>(query.Status, true, out var parsed) ? parsed : null;
        var (page, total) = await leads.ListPageAsync(status, query.Search, query.PageNumber, query.Size, ct);
        var emails = page.Select(l => l.Email).ToList();
        var registered = await signups.RegisteredEmailsAsync(emails, ct);
        return new(page.Select(l => BrandLeadRow.From(l, registered.Contains(l.Email))).ToList(), total, query.PageNumber, query.Size);
    }
}
