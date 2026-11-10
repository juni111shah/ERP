using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>One asset category.</summary>
public sealed record GetAssetCategoryQuery(Guid Id) : IQuery<AssetCategoryDto>, IRequirePermission { public string RequiredPermission => Permissions.CategoriesRead; }
