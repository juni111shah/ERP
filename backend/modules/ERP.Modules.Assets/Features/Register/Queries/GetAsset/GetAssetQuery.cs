using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>One asset of the register.</summary>
public sealed record GetAssetQuery(Guid Id) : IQuery<AssetDto>, IRequirePermission { public string RequiredPermission => Permissions.RegisterRead; }
