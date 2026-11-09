using ERP.Application.Common;

namespace ERP.Application.Dashboard;

/// <summary>The overview of core and every enabled module. <c>From</c>/<c>To</c> are optional; missing values default to the last year ending today.</summary>
public sealed record GetDashboardQuery(Guid? LegalEntityId, DateOnly? From = null, DateOnly? To = null) : IQuery<DashboardDto>;
