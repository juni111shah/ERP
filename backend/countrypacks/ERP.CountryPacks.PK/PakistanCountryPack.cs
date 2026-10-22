using ERP.Application.Customization;
using ERP.Application.Templates;
using ERP.Domain.Customization;
using ERP.Domain.Finance;

namespace ERP.CountryPacks.PK;

/// <summary>Pakistan: tax codes and the registration identifiers businesses there record on parties.</summary>
public sealed class PakistanCountryPack : ICountryPack
{
    private const string Section = "Pakistan details";

    public string CountryCode => "PK";
    public string Name => "Pakistan";
    public string DefaultCurrency => "PKR";
    public string DefaultTimeZone => "Asia/Karachi";

    public IReadOnlyList<TaxTemplate> Taxes { get; } =
    [
        new("GST18", "Sales tax 18%", TaxKind.SalesTax, 18m, true),
        new("ST-SVC", "Provincial sales tax on services", TaxKind.SalesTax, 16m),
        new("EXEMPT", "Exempt", TaxKind.SalesTax, 0m, IsRecoverable: false),
    ];

    public IReadOnlyList<CustomFieldSeed> CustomFields { get; } =
    [
        new("party", "cf_pk_ntn", "National tax number (NTN)", CustomFieldDataType.Text,
            Options: new() { MaxLength = 9, Pattern = @"^\d{7}-?\d$" }, Section: Section, HelpText: "7 digits and a check digit.", Order: 900, IsSearchable: true),
        new("party", "cf_pk_cnic", "CNIC", CustomFieldDataType.Text,
            Options: new() { MaxLength = 15, Pattern = @"^\d{5}-\d{7}-\d$" }, Section: Section, HelpText: "Format 12345-1234567-1.", Order: 910, IsSearchable: false),
        new("party", "cf_pk_strn", "Sales tax registration (STRN)", CustomFieldDataType.Text,
            Options: new() { MaxLength = 20 }, Section: Section, HelpText: null, Order: 920, IsSearchable: false),
    ];
}
