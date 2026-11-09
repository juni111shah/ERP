using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Work orders reported in the period per property and category, with open counts and estimated and actual costs.</summary>
public sealed record GetMaintenanceCostsQuery(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<MaintenanceCostsDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
