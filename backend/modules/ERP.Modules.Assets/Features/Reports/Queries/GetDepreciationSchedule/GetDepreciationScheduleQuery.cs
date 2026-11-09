using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>Depreciation by month across a legal entity's assets: posted runs up to the last run, and what the schedules still expect after it.</summary>
public sealed record GetDepreciationScheduleQuery(DateOnly From, DateOnly To, Guid? LegalEntityId = null) : IQuery<DepreciationForecastDto>, IRequirePermission { public string RequiredPermission => Permissions.ReportsRead; }
