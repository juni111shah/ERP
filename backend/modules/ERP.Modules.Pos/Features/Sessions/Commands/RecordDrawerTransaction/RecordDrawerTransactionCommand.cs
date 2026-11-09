using ERP.Application.Common;
using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

/// <summary>
/// Records cash put into or taken out of the drawer of an open shift.
/// PerformedByUserId is ignored: the movement is attributed to the signed-in caller. AccountId is the other side in the ledger:
/// a pay-in or pay-out always posts (to the register's account unless one is named here); a float or a safe drop only posts when an account is named, otherwise the cash stayed inside cash on hand.
/// </summary>
public record RecordDrawerTransactionCommand(
    Guid SessionId,
    CashMovementType Type,
    decimal Amount,
    string Reason,
    Guid? PerformedByUserId,
    Guid? AccountId = null) : ICommand<CashDrawerTransactionDto>;
