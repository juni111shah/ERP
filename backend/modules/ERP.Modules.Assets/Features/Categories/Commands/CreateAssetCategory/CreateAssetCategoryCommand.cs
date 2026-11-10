using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>Creates an asset category with its depreciation defaults and accounts.</summary>
public sealed record CreateAssetCategoryCommand(AssetCategoryInput Input) : ICommand<AssetCategoryDto>, IRequirePermission { public string RequiredPermission => Permissions.CategoriesManage; }
