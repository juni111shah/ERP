using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>The month-by-month schedule of one asset, marking what has been posted.</summary>
public sealed record GetAssetScheduleQuery(Guid AssetId) : IQuery<AssetScheduleDto>, IRequirePermission { public string RequiredPermission => Permissions.ReportsRead; }
