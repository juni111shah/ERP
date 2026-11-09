using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Catalog;

/// <summary>Updates an item; a null barcode or category leaves it unchanged.</summary>
public sealed record UpdateItemCommand(Guid Id, ItemInput Input) : ICommand<ItemDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ItemsWrite;
}
