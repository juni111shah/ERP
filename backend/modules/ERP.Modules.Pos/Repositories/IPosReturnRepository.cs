using ERP.Application.Abstractions;
using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Repositories;

/// <summary>One refund given back on a sale: the tender and the amount.</summary>
public sealed record PosRefundAmount(PosPaymentMethod Method, decimal Amount);

/// <summary>What one returned line gave back, net and tax.</summary>
public sealed record PosReturnedLineAmount(Guid OrderLineId, decimal NetAmount, decimal TaxAmount);

/// <summary>Data access for counter returns. Implemented in PosReturnRepository.cs.</summary>
public interface IPosReturnRepository : IRepository<PosReturn>
{
    /// <summary>The return with its lines and refunds, read-only; null when not found.</summary>
    Task<PosReturn?> FindWithLinesAndRefundsAsync(Guid id, CancellationToken ct);

    /// <summary>Every refund the returns of a sale gave back.</summary>
    Task<List<PosRefundAmount>> ListRefundsOfOrderAsync(Guid orderId, CancellationToken ct);

    /// <summary>What every returned line of a sale's returns gave back.</summary>
    Task<List<PosReturnedLineAmount>> ListReturnedLinesOfOrderAsync(Guid orderId, CancellationToken ct);

    /// <summary>The returns given in a shift, with their refunds.</summary>
    Task<List<PosReturn>> ListForSessionAsync(Guid sessionId, CancellationToken ct);

    /// <summary>The returns created in the range, with their refunds.</summary>
    Task<List<PosReturn>> ListInRangeAsync(DateTime start, DateTime end, Guid? legalEntityId, Guid? registerId, CancellationToken ct);

    /// <summary>The total of the returns created since <paramref name="start"/>, rounded to cents.</summary>
    Task<decimal> TotalSinceAsync(Guid? legalEntityId, DateTime start, CancellationToken ct);
}
