using ERP.Application.Abstractions;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Repositories;

/// <summary>How much of a bill line one return line took back.</summary>
public sealed record ReturnedBillLine(Guid? BillLineId, decimal Quantity);

/// <summary>Data access for purchase returns. Implemented in PurchaseReturnRepository.cs.</summary>
public interface IPurchaseReturnRepository : IRepository<PurchaseReturn>
{
    /// <summary>The return with its lines and bill shares, tracked for changes. Throws NotFoundException.</summary>
    Task<PurchaseReturn> GetWithLinesAndSharesForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The lines of the bill's other posted returns.</summary>
    Task<List<ReturnedBillLine>> ListPostedLinesOfBillAsync(Guid billId, Guid exceptReturnId, CancellationToken ct);
}
