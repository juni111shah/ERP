using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>What the real estate reports read from customer receipts. Implemented in RealEstateSettlementRepository.cs.</summary>
public interface IRealEstateSettlementRepository : IRepository<Settlement>
{
    /// <summary>Every allocation of the issued customer receipts dated from <paramref name="from"/> to <paramref name="to"/>.</summary>
    Task<List<ReceiptAllocation>> ListReceivedAllocationsAsync(DateOnly from, DateOnly to, CancellationToken ct);
}

/// <summary>What one receipt allocated to one invoice.</summary>
public sealed record ReceiptAllocation(Guid? InvoiceId, decimal Amount);
