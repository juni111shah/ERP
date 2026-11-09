using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The invoices to every client (or one), newest first, with search and a state filter ("overdue" included).</summary>
public sealed record ListPlatformInvoicesQuery : PagedQuery, IQuery<PagedResult<PlatformInvoiceRow>>
{
    public string? Status { get; init; }
    public Guid? OrgId { get; init; }
}
