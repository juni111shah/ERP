using System.Text.Json.Nodes;
using ERP.Domain.Organizations;

namespace ERP.Application.Organizations;

public sealed record LegalEntityInput(
    string Name, string? LegalName, string CountryCode, string BaseCurrency, string? TaxRegistrationNo, bool IsTaxRegistered,
    string? RegistrationNo, string? Address, string? Email, string? Phone, bool IsActive = true, JsonObject? CustomFields = null);

public sealed record LegalEntityDto(
    Guid Id, string Code, string Name, string? LegalName, string CountryCode, string BaseCurrency, string? TaxRegistrationNo,
    bool IsTaxRegistered, string? RegistrationNo, string? Address, string? Email, string? Phone, DateOnly? BooksLockedTo, bool IsActive,
    JsonObject CustomFields)
{
    public static LegalEntityDto From(LegalEntity e, JsonObject customFields) => new(
        e.Id, e.Code, e.Name, e.LegalName, e.CountryCode, e.BaseCurrency, e.TaxRegistrationNo, e.IsTaxRegistered, e.RegistrationNo,
        e.Address, e.Email, e.Phone, e.BooksLockedTo, e.IsActive, customFields);
}
