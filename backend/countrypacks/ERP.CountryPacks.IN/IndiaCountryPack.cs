using ERP.Application.Customization;
using ERP.Application.Templates;
using ERP.Domain.Customization;
using ERP.Domain.Finance;

namespace ERP.CountryPacks.IN;

/// <summary>India: tax codes and the registration identifiers businesses there record on parties.</summary>
public sealed class IndiaCountryPack : ICountryPack
{
    private const string Section = "India details";

    public string CountryCode => "IN";
    public string Name => "India";
    public string DefaultCurrency => "INR";
    public string DefaultTimeZone => "Asia/Kolkata";

    public IReadOnlyList<TaxTemplate> Taxes { get; } =
    [
        new("GST18", "GST 18%", TaxKind.Gst, 18m, true),
        new("GST5", "GST 5%", TaxKind.Gst, 5m),
        new("GST0", "GST nil", TaxKind.Gst, 0m),
    ];

    public IReadOnlyList<CustomFieldSeed> CustomFields { get; } =
    [
        new("party", "cf_in_gstin", "GSTIN", CustomFieldDataType.Text,
            Options: new() { MaxLength = 15, Pattern = @"^\d{2}[A-Z]{5}\d{4}[A-Z][1-9A-Z]Z[0-9A-Z]$" }, Section: Section, HelpText: "15 characters, for example 27ABCDE1234F1Z5.", Order: 900, IsSearchable: true),
        new("party", "cf_in_pan", "PAN", CustomFieldDataType.Text,
            Options: new() { MaxLength = 10, Pattern = @"^[A-Z]{5}\d{4}[A-Z]$" }, Section: Section, HelpText: "10 characters, for example ABCDE1234F.", Order: 910, IsSearchable: false),
    ];
}
