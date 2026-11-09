using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Catalog;

/// <summary>Creates an item (product or service) with a unique SKU and barcode.</summary>
public sealed record CreateItemCommand(ItemInput Input) : ICommand<ItemDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ItemsWrite;
}
