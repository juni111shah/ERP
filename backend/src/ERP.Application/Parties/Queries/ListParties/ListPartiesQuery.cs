using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Parties;

/// <summary>Generic paged query (search / filter / sort) plus a role shortcut (<c>?role=customer</c>).</summary>
public sealed record ListPartiesQuery : PagedQuery, IQuery<PagedResult<PartyDto>>, IRequirePermission
{
    /// <summary>Role shortcut (case-insensitive): customer, vendor, employee, other.</summary>
    public string? Role { get; init; }
    public string RequiredPermission => CorePermissions.PartiesRead;
}
