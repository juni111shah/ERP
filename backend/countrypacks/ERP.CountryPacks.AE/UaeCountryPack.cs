using ERP.Application.Customization;
using ERP.Application.Templates;
using ERP.Domain.Customization;
using ERP.Domain.Finance;

namespace ERP.CountryPacks.AE;

/// <summary>United Arab Emirates: VAT codes and the identifiers UAE businesses record on parties.</summary>
public sealed class UaeCountryPack : ICountryPack
{
    private const string Section = "UAE details";

    public string CountryCode => "AE";
    public string Name => "United Arab Emirates";

    public IReadOnlyList<TaxTemplate> Taxes { get; } =
    [
        new("VAT5", "VAT 5%", TaxKind.Vat, 5m, true),
        new("VAT0", "VAT zero-rated", TaxKind.Vat, 0m),
        new("EXEMPT", "Exempt", TaxKind.Vat, 0m, IsRecoverable: false),
    ];

    public IReadOnlyList<CustomFieldSeed> CustomFields { get; } =
    [
        new("party", "cf_ae_trn", "Tax registration number (TRN)", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^\d{15}$", MaxLength = 15 }, Section: Section, HelpText: "15 digits, issued by the Federal Tax Authority.",
            Order: 900, IsSearchable: true),
        new("party", "cf_ae_emirates_id", "Emirates ID", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^784-\d{4}-\d{7}-\d$", MaxLength = 18 }, Section: Section, HelpText: "Format 784-YYYY-NNNNNNN-C.", Order: 910),
        new("party", "cf_ae_trade_licence_no", "Trade licence number", CustomFieldDataType.Text,
            Options: new() { MaxLength = 50 }, Section: Section, Order: 920, IsSearchable: true),
        new("party", "cf_ae_licence_authority", "Licensing authority", CustomFieldDataType.Select,
            Options: new()
            {
                Choices =
                [
                    new() { Value = "ded", Label = "Department of Economic Development" },
                    new() { Value = "free_zone", Label = "Free zone authority" },
                    new() { Value = "other", Label = "Other" },
                ],
            },
            Section: Section, Order: 930),
    ];
}
