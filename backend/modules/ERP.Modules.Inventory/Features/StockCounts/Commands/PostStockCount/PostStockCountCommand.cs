using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Books one adjustment per counted difference. Posting a posted count again changes nothing.</summary>
public sealed record PostStockCountCommand(Guid Id) : ICommand<StockCountDto>, IRequirePermission { public string RequiredPermission => InventoryModule.CountsManage; }
