using ERP.Domain.Finance;

namespace ERP.Application.Templates;

public sealed record AccountTemplate(string Code, string Name, AccountType Type, string? SystemRole = null, bool IsBank = false, string? ReportingGroup = null);
public sealed record RoleTemplate(string Name, string Description, IReadOnlyList<string> Permissions, string? ModuleKey = null);
public sealed record SequenceTemplate(string DocumentType, string Prefix, string Pattern = "{prefix}-{yyyy}-{seq:6}", bool ResetYearly = true);
public sealed record TaxTemplate(string Code, string Name, TaxKind Kind, decimal Rate, bool IsDefault = false, bool IsRecoverable = true);

public enum IndustryAvailability { Available, Preview, ComingSoon }

/// <summary>
/// What onboarding provisions for an industry: suggested modules, extra chart-of-accounts lines, role
/// templates, numbering and default settings. Core ships neutral industries; modules contribute their own
/// (e.g. the Real Estate module contributes "realestate").
/// </summary>
public sealed record IndustryTemplate(
    string Key,
    string Label,
    string Description,
    IndustryAvailability Availability,
    IReadOnlyList<string> SuggestedModules,
    IReadOnlyList<AccountTemplate>? Accounts = null,
    IReadOnlyList<RoleTemplate>? Roles = null,
    IReadOnlyList<SequenceTemplate>? Sequences = null,
    IReadOnlyDictionary<string, string>? Settings = null,
    int SortOrder = 100);

/// <summary>Core, industry-neutral seed data used for every workspace.</summary>
public static class CoreTemplates
{
    public static IReadOnlyList<AccountTemplate> ChartOfAccounts { get; } =
    [
        new("1000", "Cash on hand", AccountType.Asset, "cash", ReportingGroup: "Current assets"),
        new("1010", "Bank", AccountType.Asset, "bank", IsBank: true, ReportingGroup: "Current assets"),
        new("1100", "Accounts receivable", AccountType.Asset, "ar.control", ReportingGroup: "Current assets"),
        new("1150", "Cheques in hand", AccountType.Asset, "instruments.received", ReportingGroup: "Current assets"),
        new("1200", "Input tax recoverable", AccountType.Asset, "tax.input", ReportingGroup: "Current assets"),
        new("1300", "Inventory", AccountType.Asset, "inventory", ReportingGroup: "Current assets"),
        new("1400", "Prepayments", AccountType.Asset, ReportingGroup: "Current assets"),
        new("1500", "Fixed assets", AccountType.Asset, ReportingGroup: "Non-current assets"),
        new("1590", "Accumulated depreciation", AccountType.Asset, ReportingGroup: "Non-current assets"),
        new("2000", "Accounts payable", AccountType.Liability, "ap.control", ReportingGroup: "Current liabilities"),
        new("2100", "Output tax payable", AccountType.Liability, "tax.output", ReportingGroup: "Current liabilities"),
        new("2150", "Cheques issued", AccountType.Liability, "instruments.issued", ReportingGroup: "Current liabilities"),
        new("2300", "Accrued salaries", AccountType.Liability, "payroll.accrued", ReportingGroup: "Current liabilities"),
        new("2400", "Customer advances", AccountType.Liability, ReportingGroup: "Current liabilities"),
        new("2900", "Loans", AccountType.Liability, ReportingGroup: "Non-current liabilities"),
        new("3000", "Share capital", AccountType.Equity, ReportingGroup: "Equity"),
        new("3100", "Retained earnings", AccountType.Equity, "equity.retained", ReportingGroup: "Equity"),
        new("3900", "Opening balance equity", AccountType.Equity, "equity.opening", ReportingGroup: "Equity"),
        new("4000", "Sales revenue", AccountType.Income, "income.sales", ReportingGroup: "Revenue"),
        new("4100", "Service revenue", AccountType.Income, "income.services", ReportingGroup: "Revenue"),
        new("4900", "Other income", AccountType.Income, ReportingGroup: "Other income"),
        new("5000", "Cost of goods sold", AccountType.Expense, "cogs", ReportingGroup: "Cost of sales"),
        new("6000", "Salaries and wages", AccountType.Expense, ReportingGroup: "Operating expenses"),
        new("6100", "Premises expense", AccountType.Expense, ReportingGroup: "Operating expenses"),
        new("6200", "Utilities", AccountType.Expense, ReportingGroup: "Operating expenses"),
        new("6300", "Bank charges", AccountType.Expense, ReportingGroup: "Operating expenses"),
        new("6400", "Depreciation", AccountType.Expense, ReportingGroup: "Operating expenses"),
        new("6900", "General expenses", AccountType.Expense, ReportingGroup: "Operating expenses"),
        new("7900", "Exchange gain or loss", AccountType.Expense, "fx.gainloss", ReportingGroup: "Other expenses"),
    ];

    public static IReadOnlyList<SequenceTemplate> Sequences { get; } =
    [
        new("journal", "JV"),
        new("invoice", "INV"),
        new("bill", "BILL"),
        new("receipt", "RCT"),
        new("payment", "PAY"),
        new("instrument", "CHQ"),
        new("party", "PTY", "{prefix}-{seq:5}", false),
        new("item", "ITM", "{prefix}-{seq:5}", false),
        new("legalentity", "ENT", "{prefix}-{seq:4}", false),
        new("approval", "APR"),
    ];

    private static readonly string[] FinanceRead =
    [
        Security.CorePermissions.AccountsRead, Security.CorePermissions.JournalsRead, Security.CorePermissions.ChequesRead,
        Security.CorePermissions.TaxRead, Security.CorePermissions.CurrenciesRead, Security.CorePermissions.ReportsRead,
    ];

    private static readonly string[] CoreRead =
    [
        Security.CorePermissions.EntitiesRead, Security.CorePermissions.BusinessUnitsRead, Security.CorePermissions.PartiesRead,
        Security.CorePermissions.ItemsRead, Security.CorePermissions.AttachmentsRead, Security.CorePermissions.CustomFieldsRead,
        Security.CorePermissions.SettingsRead,
    ];

    /// <summary>Generic built-in roles. Industry roles (e.g. a leasing manager) come from industry templates.</summary>
    public static IReadOnlyList<RoleTemplate> Roles { get; } =
    [
        new("Org admin", "Full access to everything in the workspace", [Security.CorePermissions.All]),
        new("Finance manager", "Runs finance, approves and sees sensitive values",
            [.. CoreRead, .. FinanceRead, Security.CorePermissions.AccountsWrite, Security.CorePermissions.JournalsWrite,
             Security.CorePermissions.JournalsPost, Security.CorePermissions.ChequesWrite, Security.CorePermissions.ChequesApprove,
             Security.CorePermissions.TaxWrite, Security.CorePermissions.CurrenciesWrite, Security.CorePermissions.PartiesWrite,
             Security.CorePermissions.ItemsWrite, Security.CorePermissions.AttachmentsWrite, Security.CorePermissions.ApprovalsDecide,
             Security.CorePermissions.AuditRead, Security.CorePermissions.SensitiveRead]),
        new("Accountant", "Day-to-day bookkeeping",
            [.. CoreRead, .. FinanceRead, Security.CorePermissions.JournalsWrite, Security.CorePermissions.ChequesWrite,
             Security.CorePermissions.PartiesWrite, Security.CorePermissions.ItemsWrite, Security.CorePermissions.AttachmentsWrite]),
        new("Approver", "Reads everything and decides approvals", [.. CoreRead, .. FinanceRead, Security.CorePermissions.ApprovalsDecide]),
        new("Viewer", "Read-only access", [.. CoreRead, .. FinanceRead]),
    ];

    /// <summary>Neutral industries offered at onboarding. Suggested module keys that are not installed are ignored.</summary>
    public static IReadOnlyList<IndustryTemplate> Industries { get; } =
    [
        new("generic", "General business / other", "Core ERP only; enable modules later.", IndustryAvailability.Available, [], SortOrder: 999),
        new("retail", "Retail and e-commerce", "Stores, stock and point of sale.", IndustryAvailability.Preview, ["inventory", "sales", "pos", "retail"], SortOrder: 20),
        new("distribution", "Wholesale and distribution", "Purchasing, stock and sales orders.", IndustryAvailability.Preview, ["inventory", "sales", "purchasing", "distribution"], SortOrder: 30),
        new("manufacturing", "Manufacturing", "Bills of materials, production and stock.", IndustryAvailability.Preview, ["inventory", "purchasing", "manufacturing"], SortOrder: 40),
        new("services", "Professional services", "Projects, time and billing.", IndustryAvailability.Preview, ["projects", "crm", "services"], SortOrder: 50),
        new("hr-only", "HR and payroll", "Employees, attendance and payroll.", IndustryAvailability.ComingSoon, ["hr"], SortOrder: 60),
    ];
}

/// <summary>Country tax packs. Rates are data, never hard-coded in posting logic.</summary>
public static class TaxTemplates
{
    public static IReadOnlyList<TaxTemplate> ForCountry(string countryCode) => countryCode.ToUpperInvariant() switch
    {
        "AE" => [new("VAT5", "VAT 5%", TaxKind.Vat, 5m, true), new("VAT0", "VAT zero-rated", TaxKind.Vat, 0m), new("EXEMPT", "Exempt", TaxKind.Vat, 0m, IsRecoverable: false)],
        "SA" => [new("VAT15", "VAT 15%", TaxKind.Vat, 15m, true), new("VAT0", "VAT zero-rated", TaxKind.Vat, 0m), new("EXEMPT", "Exempt", TaxKind.Vat, 0m, IsRecoverable: false)],
        "PK" => [new("GST18", "Sales tax 18%", TaxKind.SalesTax, 18m, true), new("ST-SVC", "Provincial sales tax on services", TaxKind.SalesTax, 16m), new("EXEMPT", "Exempt", TaxKind.SalesTax, 0m, IsRecoverable: false)],
        "IN" => [new("GST18", "GST 18%", TaxKind.Gst, 18m, true), new("GST5", "GST 5%", TaxKind.Gst, 5m), new("GST0", "GST nil", TaxKind.Gst, 0m)],
        "GB" => [new("VAT20", "VAT 20%", TaxKind.Vat, 20m, true), new("VAT5", "VAT reduced 5%", TaxKind.Vat, 5m), new("VAT0", "VAT zero-rated", TaxKind.Vat, 0m)],
        "US" => [new("ST", "Sales tax (set your rate)", TaxKind.SalesTax, 0m, true, false), new("EXEMPT", "Exempt", TaxKind.SalesTax, 0m, IsRecoverable: false)],
        _ => [new("STD", "Standard tax (set your rate)", TaxKind.Other, 0m, true), new("EXEMPT", "Exempt", TaxKind.Other, 0m, IsRecoverable: false)],
    };

    public static IReadOnlyList<(string Code, string Name, string Symbol, int Decimals)> Currencies { get; } =
    [
        ("AED", "UAE dirham", "AED", 2), ("SAR", "Saudi riyal", "SAR", 2), ("PKR", "Pakistani rupee", "Rs", 2),
        ("INR", "Indian rupee", "₹", 2), ("USD", "US dollar", "$", 2), ("EUR", "Euro", "€", 2), ("GBP", "Pound sterling", "£", 2),
        ("KWD", "Kuwaiti dinar", "KWD", 3), ("OMR", "Omani rial", "OMR", 3), ("QAR", "Qatari riyal", "QAR", 2),
    ];
}
