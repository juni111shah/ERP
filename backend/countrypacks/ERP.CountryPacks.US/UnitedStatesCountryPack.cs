using ERP.Application.Customization;
using ERP.Application.Templates;
using ERP.Domain.Customization;
using ERP.Domain.Finance;

namespace ERP.CountryPacks.US;

/// <summary>United States: tax codes and the registration identifiers businesses there record on parties.</summary>
public sealed class UnitedStatesCountryPack : ICountryPack
{
    private const string Section = "United States details";

    public string CountryCode => "US";
    public string Name => "United States";
    public string DefaultCurrency => "USD";
    public string DefaultTimeZone => "America/New_York";

    public TaxReturnDefinition? TaxReturn => UsTaxReturn.Definition;
    public IReadOnlyList<InvoiceField> InvoiceRequiredFields { get; } = [];

    public IReadOnlyList<TaxTemplate> Taxes { get; } =
    [
        new("ST", "Sales tax (set your rate)", TaxKind.SalesTax, 0m, true, false),
        new("EXEMPT", "Exempt", TaxKind.SalesTax, 0m, IsRecoverable: false),
    ];

    public IReadOnlyList<CustomFieldSeed> CustomFields { get; } =
    [
        new("party", "cf_us_ein", "Employer identification number (EIN)", CustomFieldDataType.Text,
            Options: new() { MaxLength = 10, Pattern = @"^\d{2}-\d{7}$" }, Section: Section, HelpText: "Format 12-3456789.", Order: 900, IsSearchable: true),
    ];
}
