using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Items at or below their reorder level with a suggested order quantity (the larger of the reorder quantity and the shortfall to the level).</summary>
public sealed record GetReorderSuggestionsQuery : IQuery<ReorderDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockRead; }
