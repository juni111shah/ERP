using ERP.Application.Abstractions;
using ERP.Application.Portal;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for settlements (customer receipts and vendor payments). Implemented in ERP.Infrastructure/Repositories/Finance/SettlementRepository.cs.</summary>
public interface ISettlementRepository : IRepository<Settlement>
{
    /// <summary>Customer receipts with an allocation to <paramref name="invoiceId"/>, oldest first, allocations included.</summary>
    Task<List<Settlement>> ListReceiptsAllocatedToInvoiceAsync(Guid invoiceId, CancellationToken ct);

    /// <summary>A customer receipt with its allocations, read-only. Throws NotFoundException (also for a vendor payment).</summary>
    Task<Settlement> GetReceiptWithAllocationsAsync(Guid id, CancellationToken ct);

    /// <summary>A vendor payment with its allocations, read-only. Throws NotFoundException (also for a customer receipt).</summary>
    Task<Settlement> GetPaymentWithAllocationsAsync(Guid id, CancellationToken ct);

    /// <summary>The latest 200 issued receipts from a portal customer, newest first.</summary>
    Task<List<PortalReceiptDto>> ListIssuedReceiptsForPortalAsync(Guid partyId, CancellationToken ct);

    /// <summary>The unallocated amounts of the party's issued receipts that still have some.</summary>
    Task<List<decimal>> ListUnallocatedReceiptAmountsAsync(Guid partyId, CancellationToken ct);

    /// <summary>The latest 200 issued payments to a portal vendor, newest first.</summary>
    Task<List<PortalPaymentDto>> ListIssuedPaymentsForPortalAsync(Guid partyId, CancellationToken ct);
}
