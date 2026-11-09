using ERP.Domain.Organizations;

namespace ERP.Application.Organizations;

/// <summary>Copies a legal entity's input onto the entity. Shared by create and update.</summary>
internal static class LegalEntityFields
{
    public const string EntityType = "legalentity";

    public static void Map(LegalEntityInput i, LegalEntity e)
    {
        e.Name = i.Name.Trim(); e.LegalName = i.LegalName; e.CountryCode = i.CountryCode.ToUpperInvariant(); e.BaseCurrency = i.BaseCurrency.ToUpperInvariant();
        e.TaxRegistrationNo = i.TaxRegistrationNo; e.IsTaxRegistered = i.IsTaxRegistered; e.RegistrationNo = i.RegistrationNo; e.Address = i.Address;
        e.Email = i.Email; e.Phone = i.Phone; e.IsActive = i.IsActive;
    }
}
