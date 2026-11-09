using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory;

/// <summary>Creates a warehouse with a code unique in the workspace.</summary>
public sealed record CreateWarehouseCommand(WarehouseInput Input) : ICommand<WarehouseDto>, IRequirePermission
{
    public string RequiredPermission => InventoryModule.WarehousesWrite;
}
