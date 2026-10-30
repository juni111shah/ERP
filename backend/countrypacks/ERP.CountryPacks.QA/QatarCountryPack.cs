using ERP.Application.Customization;
using ERP.Application.Templates;
using ERP.Domain.Customization;
using ERP.Domain.Finance;

namespace ERP.CountryPacks.QA;

/// <summary>Qatar: no VAT is levied, so the pack provides a 0% default and an exempt code, plus the identifiers Qatari businesses record on parties and employees.</summary>
public sealed class QatarCountryPack : ICountryPack
{
    private const string Section = "Qatar details";

    public string CountryCode => "QA";
    public string Name => "Qatar";
    public string DefaultCurrency => "QAR";
    public string DefaultTimeZone => "Asia/Qatar";

    public TaxReturnDefinition? TaxReturn => QatarTaxReturn.Definition;

    public IReadOnlyList<TaxTemplate> Taxes { get; } =
    [
        new("NOVAT", "No VAT", TaxKind.Vat, 0m, true),
        new("EXEMPT", "Exempt", TaxKind.Vat, 0m, IsRecoverable: false),
    ];

    /// <summary>Qatar Labour Law (Law 14 of 2004, art. 54): three weeks' basic wage (21 days) for each year of service; nothing before one year of service.</summary>
    public EndOfServiceRule? EndOfService { get; } = new(
        "3 weeks' (21 days') basic per year of service (Qatar Labour Law)", years => 21m * years);

    public IReadOnlyList<CustomFieldSeed> CustomFields { get; } =
    [
        new("hr.employee", "cf_qa_qid", "Qatar ID (QID)", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^\d{11}$", MaxLength = 11 }, Section: Section, HelpText: "11 digits, issued by the Ministry of Interior.", Order: 900, IsSearchable: true),
        new("hr.employee", "cf_qa_wps_bank_short_code", "WPS bank short code", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^[A-Z]{4}$", MaxLength = 4 }, Section: Section, HelpText: "4 letters, used for the Wage Protection System (for example QNBA).", Order: 910),
        new("party", "cf_qa_commercial_registration_no", "Commercial registration (CR) number", CustomFieldDataType.Text,
            Options: new() { MaxLength = 20 }, Section: Section, HelpText: "Issued by the Ministry of Commerce and Industry.", Order: 900, IsSearchable: true),
        new("party", "cf_qa_tax_card_no", "Tax card number", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^\d{6,10}$", MaxLength = 10 }, Section: Section, HelpText: "Issued by the General Tax Authority.", Order: 910, IsSearchable: true),
        new("party", "cf_qa_qid", "Qatar ID (QID)", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^\d{11}$", MaxLength = 11 }, Section: Section, HelpText: "11 digits (individuals).", Order: 920),

        // Real estate (applied only where the real estate module is installed): tenancy registration and property identifiers.
        new("realestate.lease", "cf_qa_ejar_no", "Ejar registration no.", CustomFieldDataType.Text,
            Options: new() { MaxLength = 30 }, Section: "Tenancy registration", HelpText: "Rental contracts register on the Ejar platform.", Order: 900, IsSearchable: true, ShowInList: true),
        new("realestate.unit", "cf_qa_kahramaa_no", "Kahramaa electricity / water no.", CustomFieldDataType.Text,
            Options: new() { MaxLength = 30 }, Section: Section, Order: 900, IsSearchable: true),
        new("realestate.property", "cf_qa_municipality", "Municipality", CustomFieldDataType.Select,
            Options: new()
            {
                Choices =
                [
                    new() { Value = "doha", Label = "Doha" },
                    new() { Value = "al_rayyan", Label = "Al Rayyan" },
                    new() { Value = "al_wakra", Label = "Al Wakra" },
                    new() { Value = "umm_salal", Label = "Umm Salal" },
                    new() { Value = "al_khor", Label = "Al Khor and Al Thakhira" },
                    new() { Value = "al_daayen", Label = "Al Daayen" },
                    new() { Value = "al_shamal", Label = "Al Shamal" },
                    new() { Value = "al_shahaniya", Label = "Al Shahaniya" },
                ],
            },
            Section: Section, Order: 900),
        new("realestate.property", "cf_qa_zone_no", "Zone no.", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^\d{1,3}$", MaxLength = 3 }, Section: Section, HelpText: "Qatar address zone (1-3 digits).", Order: 910),
        new("realestate.property", "cf_qa_plot_no", "Plot / title deed no.", CustomFieldDataType.Text,
            Options: new() { MaxLength = 50 }, Section: Section, Order: 920, IsSearchable: true),
    ];
}
