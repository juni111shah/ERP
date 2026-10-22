using System.Text.Json.Nodes;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record AccountDto(
    Guid Id, string Code, string Name, AccountType Type, string? ReportingGroup, string? SystemRole, bool IsBank, bool IsSystem, bool IsActive,
    Guid? ParentId, bool IsPostable, string? CurrencyCode, JsonObject CustomFields)
{
    public static AccountDto From(Account a, JsonObject customFields) => new(
        a.Id, a.Code, a.Name, a.Type, a.ReportingGroup, a.SystemRole, a.IsBank, a.IsSystem, a.IsActive, a.ParentId, a.IsPostable, a.CurrencyCode, customFields);
}

public sealed record TaxCodeDto(Guid Id, string Code, string Name, TaxKind Kind, decimal Rate, bool IsDefault, bool IsRecoverable, bool IsActive);
public sealed record CurrencyDto(string Code, string Name, string? Symbol, int Decimals);
