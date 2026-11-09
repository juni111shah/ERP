using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>Edits an asset category.</summary>
public sealed record UpdateAssetCategoryCommand(Guid Id, AssetCategoryInput Input) : ICommand<AssetCategoryDto>, IRequirePermission { public string RequiredPermission => Permissions.CategoriesManage; }
