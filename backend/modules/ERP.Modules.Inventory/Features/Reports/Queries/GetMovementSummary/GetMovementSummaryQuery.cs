using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Per item over a period: opening, receipts, issues, adjustments and closing quantity, with closing value. Transfers net to zero across warehouses and are left out.</summary>
public sealed record GetMovementSummaryQuery(DateOnly From, DateOnly To) : IQuery<MovementSummaryDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockRead; }
