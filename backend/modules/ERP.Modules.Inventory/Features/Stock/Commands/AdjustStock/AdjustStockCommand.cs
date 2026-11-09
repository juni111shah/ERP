using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Adds or removes stock in a warehouse and posts the value against the offset (default: cost of goods sold) account.</summary>
public sealed record AdjustStockCommand(AdjustStockInput Input) : ICommand<MovementDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockWrite; }
