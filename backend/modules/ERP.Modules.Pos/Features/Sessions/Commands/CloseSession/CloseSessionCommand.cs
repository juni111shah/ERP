using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>Closes a shift with the counted cash and posts any cash over or short. ClosedByUserId is ignored: the closing user is the signed-in caller.</summary>
public record CloseSessionCommand(
    Guid SessionId,
    decimal ActualCash,
    Guid? ClosedByUserId,
    string? Notes) : ICommand<PosSessionDto>;
