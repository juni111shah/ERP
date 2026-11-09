using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>Deletes an asset category that has no assets.</summary>
public sealed record DeleteAssetCategoryCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => Permissions.CategoriesManage; }
