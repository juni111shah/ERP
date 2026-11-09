using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>
/// Takes goods back from a completed sale and gives the money back the way it came.
/// SessionId is the shift giving the money back, which need not be the shift that made the sale.
/// </summary>
public record CreatePosReturnCommand(
    Guid OrderId,
    Guid SessionId,
    string Reason,
    List<PosReturnLineInput> Lines,
    List<PosRefundInput> Refunds) : ICommand<PosReturnDto>;
