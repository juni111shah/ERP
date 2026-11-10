using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Modules.Pos.Repositories;

/// <summary>The invoices and credit notes the till raises for sales on account. Implemented in PosInvoiceRepository.cs.</summary>
public interface IPosInvoiceRepository : IRepository<Invoice>
{
    /// <summary>The invoice, tracked for changes; null when not found.</summary>
    Task<Invoice?> FindForUpdateAsync(Guid id, CancellationToken ct);
}
