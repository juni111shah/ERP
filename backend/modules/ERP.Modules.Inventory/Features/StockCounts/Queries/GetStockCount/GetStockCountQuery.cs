using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>One stock count with its lines.</summary>
public sealed record GetStockCountQuery(Guid Id) : IQuery<StockCountDto>, IRequirePermission { public string RequiredPermission => InventoryModule.CountsRead; }
