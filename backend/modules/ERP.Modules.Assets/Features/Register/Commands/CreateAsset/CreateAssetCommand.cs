using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>Adds a draft asset to the register, numbered from the fixed-asset sequence and depreciated by its category's method.</summary>
public sealed record CreateAssetCommand(AssetInput Input) : ICommand<AssetDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => Permissions.RegisterManage; }
