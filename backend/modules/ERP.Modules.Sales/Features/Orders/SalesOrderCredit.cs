using ERP.Application.Finance;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

/// <summary>The customer credit check of a confirmed order. Used by update order, change order status and convert quote to order.</summary>
public sealed class SalesOrderCredit(ISalesOrderRepository orders, ICreditLimitService creditLimits)
{
    /// <summary>A confirmed order counts toward the customer's credit together with their other confirmed orders not invoiced yet.</summary>
    public async Task<string?> CheckAsync(SalesOrder order, CancellationToken ct)
    {
        if (order.CustomerPartyId is not { } party) return null;
        var open = await orders.ListOpenTotalsOfCustomerAsync(party, order.LegalEntityId, order.Id, ct);
        return await creditLimits.CheckAsync(party, order.LegalEntityId, order.TotalAmount + open.Sum(), ct);
    }
}
