using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

/// <summary>Voids a completed sale of an open shift: the shift, invoice, discount code, stock and ledger entry are all taken back.</summary>
public record VoidPosOrderCommand(
    Guid OrderId,
    string Reason) : ICommand<PosOrderDto>;
