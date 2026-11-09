using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Stock and value per warehouse and item at the end of a date (movements replayed up to it).</summary>
public sealed record GetStockValuationQuery(DateOnly AsOf) : IQuery<ValuationDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockRead; }
