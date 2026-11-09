using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Items whose stock (all warehouses) is at or below their reorder level.</summary>
public sealed record GetLowStockQuery : IQuery<IReadOnlyList<LowStockRow>>, IRequirePermission { public string RequiredPermission => InventoryModule.StockRead; }
