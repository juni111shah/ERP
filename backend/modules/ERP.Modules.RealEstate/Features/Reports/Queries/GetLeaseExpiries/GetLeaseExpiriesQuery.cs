using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Active leases by the month they end (and those already past their end), with the annual rent at risk.</summary>
public sealed record GetLeaseExpiriesQuery(int Months, Guid? LegalEntityId) : IQuery<LeaseExpiriesDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
