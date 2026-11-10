using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Occupancy, contracted rent and vacant asking rent per active property.</summary>
public sealed record GetOccupancyReportQuery(Guid? LegalEntityId) : IQuery<OccupancyDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
