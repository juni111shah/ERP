namespace ERP.Application.Finance;

public sealed record BudgetLineInput(Guid AccountId, int Month, decimal Amount);
public sealed record BudgetInput(Guid LegalEntityId, string Name, int FiscalYear, string? Notes, bool IsActive, IReadOnlyList<BudgetLineInput> Lines);
