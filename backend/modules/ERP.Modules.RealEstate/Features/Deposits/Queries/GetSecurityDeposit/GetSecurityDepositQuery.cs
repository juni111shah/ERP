using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>The security deposit of a lease: agreed, received, applied, held and its movements.</summary>
public sealed record GetSecurityDepositQuery(Guid LeaseId) : IQuery<DepositDto>, IRequirePermission { public string RequiredPermission => P.LeasesRead; }
