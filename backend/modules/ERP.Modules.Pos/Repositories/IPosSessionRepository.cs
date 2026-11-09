using ERP.Application.Abstractions;
using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Repositories;

/// <summary>The notes of a shift.</summary>
public sealed record PosSessionNotes(string? Notes);

/// <summary>A register's open shift.</summary>
public sealed record PosOpenSession(Guid RegisterId, Guid Id);

/// <summary>
/// Data access for shifts (cash drawer sessions). The running totals of an open shift change in one SQL statement each, so two
/// tills never overwrite each other's figures. Implemented in PosSessionRepository.cs.
/// </summary>
public interface IPosSessionRepository : IRepository<PosSession>
{
    /// <summary>The open shift of a register, read-only; null when it has none.</summary>
    Task<PosSession?> FindOpenForRegisterAsync(Guid registerId, CancellationToken ct);

    /// <summary>The open shifts of the registers.</summary>
    Task<List<PosOpenSession>> ListOpenOfRegistersAsync(List<Guid> registerIds, CancellationToken ct);

    /// <summary>The notes of a shift; null when the shift does not exist.</summary>
    Task<PosSessionNotes?> FindNotesAsync(Guid id, CancellationToken ct);

    /// <summary>The shift, read-only. Throws InvalidOperationException when it does not exist.</summary>
    Task<PosSession> FirstAsync(Guid id, CancellationToken ct);

    /// <summary>Closes the shift only while it is open, taking the difference from the committed expected cash in the same statement. Returns the rows changed.</summary>
    Task<int> CloseIfOpenAsync(Guid id, Guid closedBy, DateTimeOffset closedAt, decimal actualCash, string? notes, CancellationToken ct);

    /// <summary>Adds a float or pay-in to an open shift. Returns the rows changed (0: not open).</summary>
    Task<int> AddCashInAsync(Guid id, decimal amount, CancellationToken ct);

    /// <summary>Takes a pay-out or safe drop from an open shift whose drawer covers it (always when <paramref name="allowNegative"/>). Returns the rows changed.</summary>
    Task<int> TakeCashOutAsync(Guid id, decimal amount, bool allowNegative, CancellationToken ct);

    /// <summary>Adds a sale to an open shift. Returns the rows changed (0: not open).</summary>
    Task<int> AddSaleAsync(Guid id, decimal total, decimal cashPaid, CancellationToken ct);

    /// <summary>Takes a voided sale back out of an open shift. Returns the rows changed (0: not open).</summary>
    Task<int> VoidSaleAsync(Guid id, decimal total, decimal cashRefunded, CancellationToken ct);

    /// <summary>Adds a return to an open shift whose drawer covers the cash refund (always when <paramref name="allowNegative"/>). Returns the rows changed.</summary>
    Task<int> AddReturnAsync(Guid id, decimal total, decimal cash, bool allowNegative, CancellationToken ct);

    /// <summary>The cash over or short of the closed shifts created in the range.</summary>
    Task<List<decimal>> ListClosedCashDifferencesAsync(DateTime start, DateTime end, Guid? legalEntityId, Guid? registerId, CancellationToken ct);
}
