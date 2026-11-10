namespace ERP.Application.Finance;

public sealed record TaxSummaryRow(string Section, Guid? TaxCodeId, string Code, string Name, decimal Rate, decimal Net, decimal Tax);
public sealed record TaxSummaryDto(DateOnly From, DateOnly To, IReadOnlyList<TaxSummaryRow> Rows, decimal OutputTax, decimal InputTax, decimal NetPayable, decimal NonRecoverableTax);
