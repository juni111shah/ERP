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
    public string DefaultCurrency => "AED";
    public string DefaultTimeZone => "Asia/Dubai";

    public TaxReturnDefinition? TaxReturn => UaeTaxReturn.Definition;
    public IReadOnlyList<InvoiceField> InvoiceRequiredFields { get; } = [InvoiceField.SellerTaxNumber, InvoiceField.CustomerTaxNumber];

    public IReadOnlyList<TaxTemplate> Taxes { get; } =
    [
        new("VAT5", "VAT 5%", TaxKind.Vat, 5m, true),
        new("VAT0", "VAT zero-rated", TaxKind.Vat, 0m),
        new("EXEMPT", "Exempt", TaxKind.Vat, 0m, IsRecoverable: false),
    ];

    /// <summary>UAE Labour Law: 21 days' basic per year for the first five years, 30 days a year after that; nothing before one year of service.</summary>
    public EndOfServiceRule? EndOfService { get; } = new(
        "21 days' basic per year for the first 5 years, 30 days thereafter (UAE Labour Law)", years => 21m * Math.Min(years, 5m) + 30m * Math.Max(years - 5m, 0m));

    public IReadOnlyList<CustomFieldSeed> CustomFields { get; } =
    [
        new("hr.employee", "cf_ae_emirates_id", "Emirates ID", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^784-\d{4}-\d{7}-\d$", MaxLength = 18 }, Section: Section, HelpText: "Format 784-YYYY-NNNNNNN-C.", Order: 900),
        new("hr.employee", "cf_ae_mohre_personal_no", "MOHRE personal number", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^\d{14}$", MaxLength = 14 }, Section: Section, HelpText: "14 digits, used for the Wage Protection System.", Order: 910),
        new("hr.employee", "cf_ae_bank_routing_code", "Bank routing code", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^\d{9}$", MaxLength = 9 }, Section: Section, HelpText: "9 digits.", Order: 920),
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

        // Real estate (applied only where the real estate module is installed): tenancy registration and property identifiers.
        new("realestate.lease", "cf_ae_registration_authority", "Registration authority", CustomFieldDataType.Select,
            Options: new()
            {
                Choices =
                [
                    new() { Value = "ejari", Label = "Ejari (Dubai)" },
                    new() { Value = "tawtheeq", Label = "Tawtheeq (Abu Dhabi)" },
                    new() { Value = "other", Label = "Other emirate" },
                ],
            },
            Section: "Tenancy registration", HelpText: "Dubai leases register in Ejari, Abu Dhabi leases in Tawtheeq.", Order: 900),
        new("realestate.lease", "cf_ae_registration_no", "Ejari / Tawtheeq no.", CustomFieldDataType.Text,
            Options: new() { MaxLength = 30 }, Section: "Tenancy registration", Order: 910, IsSearchable: true, ShowInList: true),
        new("realestate.unit", "cf_ae_premise_no", "DEWA / ADDC premise no.", CustomFieldDataType.Text,
            Options: new() { MaxLength = 30 }, Section: "UAE details", Order: 900, IsSearchable: true),
        new("realestate.property", "cf_ae_title_deed_no", "Title deed no.", CustomFieldDataType.Text,
            Options: new() { MaxLength = 50 }, Section: "UAE details", Order: 900, IsSearchable: true),
        new("realestate.property", "cf_ae_makani_no", "Makani no.", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^\d{10}$", MaxLength = 10 }, Section: "UAE details", HelpText: "10 digits (Dubai).", Order: 910),
    ];
}
