using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>The occupying leases with their unit, tenant, rent, cheques held and what is outstanding.</summary>
public sealed record GetRentRollQuery(DateOnly AsOf, Guid[]? PropertyIds, Guid? LegalEntityId) : IQuery<RentRollDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
