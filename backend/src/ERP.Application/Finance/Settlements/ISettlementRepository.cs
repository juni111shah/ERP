using System.Linq.Expressions;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Application.Portal;

namespace ERP.Application.Finance;

/// <summary>Data access for settlements (customer receipts and vendor payments). Implemented in ERP.Infrastructure/Repositories/Finance/SettlementRepository.cs.</summary>
public interface ISettlementRepository : IRepository<Settlement>
{
    /// <summary>Customer receipts with an allocation to <paramref name="invoiceId"/>, oldest first, allocations included.</summary>
    Task<List<Settlement>> ListReceiptsAllocatedToInvoiceAsync(Guid invoiceId, CancellationToken ct);

    /// <summary>The customer receipt with its allocations, read-only. Throws NotFoundException.</summary>
    Task<Settlement> GetReceiptWithAllocationsAsync(Guid id, CancellationToken ct);

    /// <summary>The customer receipt with its allocations, tracked for changes. Throws NotFoundException.</summary>
    Task<Settlement> GetReceiptWithAllocationsForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The vendor payment with its allocations, read-only. Throws NotFoundException.</summary>
    Task<Settlement> GetPaymentWithAllocationsAsync(Guid id, CancellationToken ct);

    /// <summary>The vendor payment with its allocations, tracked for changes. Throws NotFoundException.</summary>
    Task<Settlement> GetPaymentWithAllocationsForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>A settlement of either direction (the one behind a cheque) with its allocations, tracked for changes. Throws NotFoundException.</summary>
    Task<Settlement> GetWithAllocationsForUpdateAsync(Guid? id, CancellationToken ct);

    /// <summary>One page of the receipts (received) or of the payments (issued). Search, filter, sort and paging stay generic.</summary>
    Task<PagedResult<TDto>> PageByDirectionAsync<TDto>(InstrumentDirection direction, PagedQuery query, Expression<Func<Settlement, TDto>> projection, CancellationToken ct);

    /// <summary>Receipts (received) or payments (issued) dated on or before <paramref name="asOf"/> that were not voided by the end of that day.</summary>
    Task<List<SettlementAmount>> ListNotVoidedAsOfAsync(InstrumentDirection direction, DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct);

    /// <summary>What those receipts or payments have allocated, per settlement.</summary>
    Task<Dictionary<Guid, decimal>> AllocatedPerSettlementAsOfAsync(InstrumentDirection direction, DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct);

    /// <summary>What the receipts not voided by the end of <paramref name="asOf"/> have allocated, per invoice.</summary>
    Task<Dictionary<Guid, decimal>> AllocatedPerInvoiceAsOfAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct);

    /// <summary>What the payments not voided by the end of <paramref name="asOf"/> have allocated, per bill.</summary>
    Task<Dictionary<Guid, decimal>> AllocatedPerBillAsOfAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct);

    /// <summary>The total of the party's statement settlements dated before <paramref name="from"/>.</summary>
    Task<decimal> StatementTotalBeforeAsync(InstrumentDirection direction, Guid partyId, DateOnly from, DateOnly to, Guid? entityId, CancellationToken ct);

    /// <summary>The party's statement settlements dated from <paramref name="from"/> to <paramref name="to"/>.</summary>
    Task<List<PartySettlementRow>> ListStatementRowsAsync(InstrumentDirection direction, Guid partyId, DateOnly from, DateOnly to, Guid? entityId, CancellationToken ct);

    /// <summary>The party's settlements voided after their period was locked, the void posted on or before <paramref name="to"/>.</summary>
    Task<List<VoidedSettlement>> ListVoidedAfterLockAsync(InstrumentDirection direction, Guid partyId, DateOnly to, Guid? entityId, CancellationToken ct);

    /// <summary>The latest 200 issued receipts from a portal customer, newest first.</summary>
    Task<List<PortalReceiptDto>> ListIssuedReceiptsForPortalAsync(Guid partyId, CancellationToken ct);

    /// <summary>The unallocated amounts of the party's issued receipts that still have some.</summary>
    Task<List<decimal>> ListUnallocatedReceiptAmountsAsync(Guid partyId, CancellationToken ct);

    /// <summary>The latest 200 issued payments to a portal vendor, newest first.</summary>
    Task<List<PortalPaymentDto>> ListIssuedPaymentsForPortalAsync(Guid partyId, CancellationToken ct);
}
