using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory;

/// <summary>Renames a warehouse, changes its address or deactivates it. The code and legal entity never change (stock movements refer to them).</summary>
public sealed record UpdateWarehouseCommand(Guid Id, WarehouseUpdateInput Input) : ICommand<WarehouseDto>, IRequirePermission
{
    public string RequiredPermission => InventoryModule.WarehousesWrite;
}
