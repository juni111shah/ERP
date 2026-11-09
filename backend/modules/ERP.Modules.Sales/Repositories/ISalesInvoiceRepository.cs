using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Modules.Sales.Repositories;

/// <summary>The invoices sales returns are raised against. Implemented in SalesInvoiceRepository.cs.</summary>
public interface ISalesInvoiceRepository : IRepository<Invoice>
{
    /// <summary>The invoice with its lines, read-only; null when not found.</summary>
    Task<Invoice?> FindWithLinesAsync(Guid id, CancellationToken ct);
}
