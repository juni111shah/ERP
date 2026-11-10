using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

public record CashDrawerTransactionDto(
    Guid Id,
    Guid SessionId,
    CashMovementType Type,
    decimal Amount,
    string Reason,
    Guid PerformedByUserId,
    DateTimeOffset Timestamp,
    string? PerformedByName = null);
