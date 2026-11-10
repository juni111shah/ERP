using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record DepositTransactionDto(Guid Id, DateOnly Date, DepositKind Kind, decimal Amount, string? Description);
public sealed record DepositDto(Guid LeaseId, decimal Agreed, decimal Received, decimal Applied, decimal Held, IReadOnlyList<DepositTransactionDto> Transactions);
public sealed record ArrearsInput(Guid InvoiceId, decimal Amount);
public sealed record SettleDepositInput(DateOnly Date, decimal Damages, string? DamagesDescription, List<ArrearsInput>? Arrears, decimal Refund, Guid? RefundBankAccountId);
