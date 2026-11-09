using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Quantity, average cost and value on hand per warehouse and item, optionally for one warehouse and/or item.</summary>
public sealed record GetStockOnHandQuery(Guid? WarehouseId, Guid? ItemId) : IQuery<StockOnHandDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockRead; }
