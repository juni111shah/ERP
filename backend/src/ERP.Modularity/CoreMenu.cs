using ERP.Application.Security;

namespace ERP.Modularity;

/// <summary>Core navigation (industry-neutral). Modules add their groups through IModule.Menu.</summary>
public static class CoreMenu
{
    public static IReadOnlyList<MenuItemDefinition> Items { get; } =
    [
        new("core.overview", "Overview", "/", "dashboard", null, 10, "workspace", "Workspace", 10),
        new("core.approvals", "Approvals", "/approvals", "check-square", null, 20, "workspace", "Workspace", 10),
        new("core.parties", "Parties", "/parties", "team", CorePermissions.PartiesRead, 10, "masterdata", "Master data", 20),
        new("core.items", "Items", "/catalog/items", "shopping", CorePermissions.ItemsRead, 20, "masterdata", "Master data", 20),
        new("finance.journals", "Journals", "/finance/journals", "book", CorePermissions.JournalsRead, 5, "finance", "Finance", 50),
        new("finance.invoices", "Invoices", "/finance/invoices", "file-text", CorePermissions.InvoicesRead, 6, "finance", "Finance", 50),
        new("finance.receipts", "Receipts", "/finance/receipts", "wallet", CorePermissions.ReceiptsRead, 7, "finance", "Finance", 50),
        new("finance.bills", "Bills", "/finance/bills", "file-text", CorePermissions.BillsRead, 6, "finance", "Finance", 50),
        new("finance.payments", "Payments", "/finance/payments", "wallet", CorePermissions.PaymentsRead, 7, "finance", "Finance", 50),
        new("finance.cheques", "Cheque register", "/finance/cheques", "credit-card", CorePermissions.ChequesRead, 8, "finance", "Finance", 50),
        new("finance.bank", "Cash and bank", "/finance/bank", "bank", CorePermissions.BankRead, 9, "finance", "Finance", 50),
        new("finance.reports", "Financial reports", "/finance/reports", "bar-chart", CorePermissions.ReportsRead, 30, "finance", "Finance", 50),
        new("finance.accounts", "Chart of accounts", "/finance/accounts", "account-book", CorePermissions.AccountsRead, 10, "finance", "Finance", 50),
        new("finance.taxcodes", "Tax codes", "/finance/tax-codes", "percentage", CorePermissions.TaxRead, 20, "finance", "Finance", 50),
        new("core.legalentities", "Legal entities", "/admin/legal-entities", "apartment", CorePermissions.EntitiesRead, 10, "admin", "Administration", 900),
        new("core.businessunits", "Business units", "/admin/business-units", "cluster", CorePermissions.BusinessUnitsRead, 20, "admin", "Administration", 900),
        new("core.users", "Users", "/admin/users", "user", CorePermissions.UsersRead, 30, "admin", "Administration", 900),
        new("core.roles", "Roles", "/admin/roles", "safety", CorePermissions.RolesRead, 40, "admin", "Administration", 900),
        new("core.customfields", "Custom fields", "/admin/custom-fields", "form", CorePermissions.CustomFieldsRead, 50, "admin", "Administration", 900),
        new("core.modules", "Modules", "/admin/modules", "appstore-add", CorePermissions.SettingsRead, 60, "admin", "Administration", 900),
        new("core.approvalrules", "Approval rules", "/admin/approval-rules", "safety", CorePermissions.SettingsRead, 75, "admin", "Administration", 900),
        new("core.settings", "Settings", "/admin/settings", "setting", CorePermissions.SettingsRead, 70, "admin", "Administration", 900),
        new("core.audit", "Audit log", "/admin/audit", "audit", CorePermissions.AuditRead, 80, "admin", "Administration", 900),
    ];
}
