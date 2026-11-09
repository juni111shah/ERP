using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The leads, newest first, with whether each has registered since.</summary>
public sealed record ListBrandLeadsQuery : PagedQuery, IQuery<PagedResult<BrandLeadRow>>
{
    public string? Status { get; init; }
}
