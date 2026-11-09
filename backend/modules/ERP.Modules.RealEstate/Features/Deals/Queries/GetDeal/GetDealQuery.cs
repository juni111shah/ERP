using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>One brokerage deal with its custom fields.</summary>
public sealed record GetDealQuery(Guid Id) : IQuery<DealDto>, IRequirePermission
{
    public string RequiredPermission => P.DealsRead;
}
