using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Inventory.Features;

/// <summary>Stock value (movements replayed up to the date) against the balance of the inventory account in the ledger, per legal entity. Difference = stock value minus ledger balance.</summary>
public sealed record GetStockGlReconciliationQuery(DateOnly AsOf, Guid? LegalEntityId) : IQuery<StockGlReconciliationDto>, IRequirePermission { public string RequiredPermission => InventoryModule.ReconciliationRead; }
