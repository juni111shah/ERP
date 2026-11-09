using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Active leases ending within the window, with their renewal state and how the rent compares with the unit's asking rent.</summary>
public sealed record GetRenewalWorkbenchQuery(int WithinDays, DateOnly AsOf) : IQuery<RenewalWorkbenchDto>, IRequirePermission { public string RequiredPermission => P.LeasesRenew; }
