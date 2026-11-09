using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

public record PosReturnLineInput(Guid OrderLineId, decimal Quantity, bool Restock = true);

public record PosRefundInput(PosPaymentMethod Method, decimal Amount, string? Reference = null);
