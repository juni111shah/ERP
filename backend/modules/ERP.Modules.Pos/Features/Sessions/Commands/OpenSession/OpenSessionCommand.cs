using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>Opens a shift on a register. CashierUserId and CashierName are ignored: the opening cashier is the signed-in caller.</summary>
public record OpenSessionCommand(
    Guid LegalEntityId,
    Guid RegisterId,
    decimal OpeningCash,
    Guid? CashierUserId,
    string? CashierName,
    string? Notes) : ICommand<PosSessionDto>;
