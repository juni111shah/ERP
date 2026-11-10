using ERP.Application.Abstractions;
using ERP.Modules.Sales.Domain;

namespace ERP.Modules.Sales.Repositories;

/// <summary>Data access for sales orders. Implemented in SalesOrderRepository.cs.</summary>
public interface ISalesOrderRepository : IRepository<SalesOrder>
{
    /// <summary>The order with its lines, tracked for changes; null when not found.</summary>
    Task<SalesOrder?> FindWithLinesForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The totals of the customer's other confirmed, processing or delivered orders of the legal entity (not invoiced yet).</summary>
    Task<List<decimal>> ListOpenTotalsOfCustomerAsync(Guid customerPartyId, Guid legalEntityId, Guid exceptOrderId, CancellationToken ct);

    /// <summary>How many orders are open (confirmed or processing).</summary>
    Task<int> CountOpenAsync(CancellationToken ct);

    /// <summary>The total of the open orders (confirmed or processing).</summary>
    Task<decimal> TotalOpenAsync(CancellationToken ct);

    /// <summary>The total of every order that is not cancelled.</summary>
    Task<decimal> TotalNotCancelledAsync(CancellationToken ct);
}
