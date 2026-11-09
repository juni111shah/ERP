using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Cancels a stock count that is not posted.</summary>
public sealed record CancelStockCountCommand(Guid Id) : ICommand<StockCountDto>, IRequirePermission { public string RequiredPermission => InventoryModule.CountsManage; }
