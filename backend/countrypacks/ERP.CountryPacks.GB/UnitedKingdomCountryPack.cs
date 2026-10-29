using ERP.Application.Customization;
using ERP.Application.Templates;
using ERP.Domain.Customization;
using ERP.Domain.Finance;

namespace ERP.CountryPacks.GB;

/// <summary>United Kingdom: tax codes and the registration identifiers businesses there record on parties.</summary>
public sealed class UnitedKingdomCountryPack : ICountryPack
{
    private const string Section = "United Kingdom details";

    public string CountryCode => "GB";
    public string Name => "United Kingdom";
    public string DefaultCurrency => "GBP";
    public string DefaultTimeZone => "Europe/London";

    public TaxReturnDefinition? TaxReturn => UkTaxReturn.Definition;
    public IReadOnlyList<InvoiceField> InvoiceRequiredFields { get; } = [InvoiceField.SellerTaxNumber];

    public IReadOnlyList<TaxTemplate> Taxes { get; } =
    [
        new("VAT20", "VAT 20%", TaxKind.Vat, 20m, true),
        new("VAT5", "VAT reduced 5%", TaxKind.Vat, 5m),
        new("VAT0", "VAT zero-rated", TaxKind.Vat, 0m),
    ];

    public IReadOnlyList<CustomFieldSeed> CustomFields { get; } =
    [
        new("party", "cf_gb_vat_no", "VAT registration number", CustomFieldDataType.Text,
            Options: new() { MaxLength = 14, Pattern = @"^(GB)?\d{9}(\d{3})?$" }, Section: Section, HelpText: "9 digits, optionally prefixed GB.", Order: 900, IsSearchable: true),
        new("party", "cf_gb_company_no", "Companies House number", CustomFieldDataType.Text,
            Options: new() { MaxLength = 8, Pattern = @"^[A-Z0-9]{8}$" }, Section: Section, HelpText: "8 characters.", Order: 910, IsSearchable: false),
    ];
}
