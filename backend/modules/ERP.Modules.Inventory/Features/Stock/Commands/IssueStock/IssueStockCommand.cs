using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Issues stock from a warehouse at its average cost and posts the value to the expense (default: cost of goods sold) account.</summary>
public sealed record IssueStockCommand(IssueStockInput Input) : ICommand<MovementDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockWrite; }
