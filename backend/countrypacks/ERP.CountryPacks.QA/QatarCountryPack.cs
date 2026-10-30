using ERP.Application.Customization;
using ERP.Application.Templates;
using ERP.Domain.Customization;
using ERP.Domain.Finance;

namespace ERP.CountryPacks.QA;

/// <summary>Qatar: no VAT is levied (so only a zero tax code), QAR, and the identifiers Qatari businesses record on parties, staff and property.</summary>
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
        new("QA0", "No VAT (Qatar)", TaxKind.Vat, 0m, true, IsRecoverable: false),
    ];

    /// <summary>Qatar Labour Law No. 14 of 2004: at least three weeks' basic wage for each year of service, after one year of service.</summary>
    public EndOfServiceRule? EndOfService { get; } = new(
        "21 days' basic per year of service (Qatar Labour Law No. 14 of 2004)", years => 21m * years);

    public IReadOnlyList<CustomFieldSeed> CustomFields { get; } =
    [
        new("hr.employee", "cf_qa_qid", "Qatar ID (QID)", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^\d{11}$", MaxLength = 11 }, Section: Section, HelpText: "11 digits.", Order: 900),
        new("hr.employee", "cf_qa_wps_bank_code", "WPS bank code", CustomFieldDataType.Text,
            Options: new() { MaxLength = 8 }, Section: Section, HelpText: "Bank code used in the Wage Protection System file.", Order: 910),
        new("party", "cf_qa_cr_no", "Commercial Registration (CR) no.", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^\d{1,10}$", MaxLength = 10 }, Section: Section, HelpText: "Issued by the Ministry of Commerce and Industry.",
            Order: 900, IsSearchable: true),
        new("party", "cf_qa_tax_card_no", "Tax card no.", CustomFieldDataType.Text,
            Options: new() { MaxLength = 20 }, Section: Section, HelpText: "Issued by the General Tax Authority.", Order: 910, IsSearchable: true),
        new("party", "cf_qa_qid", "Qatar ID (QID)", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^\d{11}$", MaxLength = 11 }, Section: Section, HelpText: "11 digits.", Order: 920),

        // Real estate (applied only where the real estate module is installed): tenancy registration and property identifiers.
        new("realestate.lease", "cf_qa_registration_authority", "Registration authority", CustomFieldDataType.Select,
            Options: new()
            {
                Choices =
                [
                    new() { Value = "ejar", Label = "Ejar (Ministry of Municipality)" },
                    new() { Value = "other", Label = "Other" },
                ],
            },
            Section: "Tenancy registration", HelpText: "Qatar tenancy contracts register on the Ejar platform.", Order: 900),
        new("realestate.lease", "cf_qa_registration_no", "Ejar contract no.", CustomFieldDataType.Text,
            Options: new() { MaxLength = 30 }, Section: "Tenancy registration", Order: 910, IsSearchable: true, ShowInList: true),
        new("realestate.unit", "cf_qa_premise_no", "Kahramaa electricity / water no.", CustomFieldDataType.Text,
            Options: new() { MaxLength = 30 }, Section: Section, Order: 900, IsSearchable: true),
        new("realestate.property", "cf_qa_title_deed_no", "Title deed no.", CustomFieldDataType.Text,
            Options: new() { MaxLength = 50 }, Section: Section, Order: 900, IsSearchable: true),
        new("realestate.property", "cf_qa_zone_street_building", "Zone / street / building no.", CustomFieldDataType.Text,
            Options: new() { Pattern = @"^\d{1,3}/\d{1,4}/\d{1,4}$", MaxLength = 14 }, Section: Section, HelpText: "National address, for example 66/850/12.", Order: 910),
    ];
}
