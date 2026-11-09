using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>End-of-service liability per employee, under the employing entity's country rule.</summary>
public sealed record GetGratuityReportQuery(DateOnly AsOf, Guid? LegalEntityId) : IQuery<GratuityReportDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
