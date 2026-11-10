using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>Edits an asset; once it is capitalised only what does not change a figure can still change.</summary>
public sealed record UpdateAssetCommand(Guid Id, AssetInput Input) : ICommand<AssetDto>, IRequirePermission { public string RequiredPermission => Permissions.RegisterManage; }
