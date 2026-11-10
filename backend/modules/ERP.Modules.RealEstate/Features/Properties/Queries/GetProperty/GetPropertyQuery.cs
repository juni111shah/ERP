using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>A property with its occupancy, contracted rent and gross yield.</summary>
public sealed record GetPropertyQuery(Guid Id) : IQuery<PropertySummaryDto>, IRequirePermission
{
    public string RequiredPermission => P.PropertiesRead;
}
