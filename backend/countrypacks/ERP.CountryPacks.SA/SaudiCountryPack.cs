using ERP.Application.Customization;
using ERP.Application.Templates;
using ERP.Domain.Customization;
using ERP.Domain.Finance;

namespace ERP.CountryPacks.SA;

/// <summary>Saudi Arabia: tax codes and the registration identifiers businesses there record on parties.</summary>
public sealed class SaudiCountryPack : ICountryPack
{
    private const string Section = "Saudi Arabia details";

    public string CountryCode => "SA";
    public string Name => "Saudi Arabia";
    public string DefaultCurrency => "SAR";
    public string DefaultTimeZone => "Asia/Riyadh";

    public IReadOnlyList<TaxTemplate> Taxes { get; } =
    [
        new("VAT15", "VAT 15%", TaxKind.Vat, 15m, true),
        new("VAT0", "VAT zero-rated", TaxKind.Vat, 0m),
        new("EXEMPT", "Exempt", TaxKind.Vat, 0m, IsRecoverable: false),
    ];

    public IReadOnlyList<CustomFieldSeed> CustomFields { get; } =
    [
        new("party", "cf_sa_vat_no", "VAT registration number", CustomFieldDataType.Text,
            Options: new() { MaxLength = 15, Pattern = @"^3\d{13}3$" }, Section: Section, HelpText: "15 digits, starting and ending with 3.", Order: 900, IsSearchable: true),
        new("party", "cf_sa_cr_no", "Commercial registration (CR) no.", CustomFieldDataType.Text,
            Options: new() { MaxLength = 10, Pattern = @"^\d{10}$" }, Section: Section, HelpText: "10 digits.", Order: 910, IsSearchable: false),
    ];
}
