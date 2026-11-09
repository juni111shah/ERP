using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Sets the reorder level and quantity of a product item.</summary>
public sealed record SetStockPolicyCommand(StockPolicyInput Input) : ICommand, IRequirePermission { public string RequiredPermission => InventoryModule.StockWrite; }
