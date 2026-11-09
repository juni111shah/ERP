using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Starts a count sheet for a warehouse: one line per item currently held there, with the system quantity.</summary>
public sealed record CreateStockCountCommand(StockCountInput Input) : ICommand<StockCountDto>, IRequirePermission { public string RequiredPermission => InventoryModule.CountsManage; }
