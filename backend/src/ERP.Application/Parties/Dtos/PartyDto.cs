using System.Text.Json.Nodes;
using ERP.Domain.Parties;

namespace ERP.Application.Parties;

public sealed record PartyDto(
    Guid Id, string Code, string Name, string Kind, string[] Roles, string? Email, string? Phone, string? TaxNumber,
    string? IdNumber, string? Iban, string? CurrencyCode, int? PaymentTermsDays, string? Address, bool IsActive, JsonObject CustomFields, Guid? PartyGroupId = null, decimal? CreditLimit = null);

public sealed record PartyInput(
    string Name, PartyKind Kind, PartyRoles Roles, string? Email, string? Phone, string? TaxNumber, string? IdNumber,
    string? Iban, string? CurrencyCode, int? PaymentTermsDays, string? Address, bool IsActive = true, JsonObject? CustomFields = null, Guid? PartyGroupId = null, decimal? CreditLimit = null, bool ClearCreditLimit = false);
