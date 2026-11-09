using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Items still in stock that have had no movement for the given number of days.</summary>
public sealed record GetSlowMovingQuery(DateOnly AsOf, int Days) : IQuery<SlowMovingDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockRead; }
