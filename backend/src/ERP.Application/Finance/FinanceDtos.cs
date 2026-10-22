using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record AccountDto(Guid Id, string Code, string Name, AccountType Type, string? ReportingGroup, string? SystemRole, bool IsBank, bool IsSystem, bool IsActive);
public sealed record TaxCodeDto(Guid Id, string Code, string Name, TaxKind Kind, decimal Rate, bool IsDefault, bool IsRecoverable, bool IsActive);
public sealed record CurrencyDto(string Code, string Name, string? Symbol, int Decimals);
