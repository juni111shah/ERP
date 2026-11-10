using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record TaxCodeInput(string Code, string Name, TaxKind Kind, decimal Rate, bool IsRecoverable = true, bool IsDefault = false, bool IsActive = true);
