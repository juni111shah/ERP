using ERP.Application.Abstractions;
using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Repositories;

/// <summary>Data access for counter sales and held tickets. Implemented in PosOrderRepository.cs.</summary>
public interface IPosOrderRepository : IRepository<PosOrder>
{
    /// <summary>The order with its lines and payments, read-only; null when not found.</summary>
    Task<PosOrder?> FindWithLinesAndPaymentsAsync(Guid id, CancellationToken ct);

    /// <summary>The order with this receipt number, with its lines and payments, read-only; null when not found.</summary>
    Task<PosOrder?> FindByNumberWithLinesAndPaymentsAsync(string number, CancellationToken ct);

    /// <summary>The order with its lines and payments, tracked for changes; null when not found.</summary>
    Task<PosOrder?> FindWithLinesAndPaymentsForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The order, tracked for changes; null when not found.</summary>
    Task<PosOrder?> FindForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The order while it is still on hold, tracked for changes; null otherwise.</summary>
    Task<PosOrder?> FindParkedForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The latest 100 held tickets (of one register, or of all), newest first, with lines and payments.</summary>
    Task<List<PosOrder>> ListParkedAsync(Guid? registerId, CancellationToken ct);

    /// <summary>Every order of a shift, with lines and payments.</summary>
    Task<List<PosOrder>> ListForSessionAsync(Guid sessionId, CancellationToken ct);

    /// <summary>The orders created in the range that are not on hold, with lines and payments.</summary>
    Task<List<PosOrder>> ListInRangeAsync(DateTime start, DateTime end, Guid? legalEntityId, Guid? registerId, CancellationToken ct);

    /// <summary>How many completed orders were placed since the shop's midnight.</summary>
    Task<int> CountCompletedSinceAsync(Guid? legalEntityId, DateTime startOfDayUtc, CancellationToken ct);

    /// <summary>The total of the completed orders placed since the shop's midnight, rounded to cents.</summary>
    Task<decimal> TotalCompletedSinceAsync(Guid? legalEntityId, DateTime startOfDayUtc, CancellationToken ct);
}
