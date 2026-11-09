using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Receives stock into a warehouse at a unit cost and posts its value against the offset account.</summary>
public sealed record ReceiveStockCommand(ReceiveStockInput Input) : ICommand<MovementDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockWrite; }
