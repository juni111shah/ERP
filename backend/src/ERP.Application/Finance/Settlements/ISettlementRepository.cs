using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for settlements (customer receipts and vendor payments). Implemented in ERP.Infrastructure/Repositories/Finance/SettlementRepository.cs.</summary>
public interface ISettlementRepository : IRepository<Settlement>
{
    /// <summary>Customer receipts with an allocation to <paramref name="invoiceId"/>, oldest first, allocations included.</summary>
    Task<List<Settlement>> ListReceiptsAllocatedToInvoiceAsync(Guid invoiceId, CancellationToken ct);
}
