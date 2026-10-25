using ERP.Application.Security;

namespace ERP.Modularity;

/// <summary>Core navigation (industry-neutral). Modules add their groups through IModule.Menu.</summary>
public static class CoreMenu
{
    public static IReadOnlyList<MenuItemDefinition> Items { get; } =
    [
        new("core.overview", "Overview", "/", "dashboard", null, 10, "workspace", "Workspace", 10),
        new("core.approvals", "Approvals", "/approvals", "check-square", null, 20, "workspace", "Workspace", 10),
        
        // Sales group
        new("sales.customers", "Customers", "/parties?type=customer", "team", CorePermissions.PartiesRead, 10, "sales", "Sales", 30),
        new("finance.invoices", "Invoices", "/finance/invoices", "file-text", CorePermissions.InvoicesRead, 20, "sales", "Sales", 30),
        new("finance.receipts", "Receipts", "/finance/receipts", "wallet", CorePermissions.ReceiptsRead, 30, "sales", "Sales", 30),
        
        // Purchases group
        new("purchases.vendors", "Vendors", "/parties?type=vendor", "team", CorePermissions.PartiesRead, 10, "purchases", "Purchases", 40),
        new("finance.bills", "Bills", "/finance/bills", "file-text", CorePermissions.BillsRead, 20, "purchases", "Purchases", 40),
        new("finance.payments", "Payments", "/finance/payments", "wallet", CorePermissions.PaymentsRead, 30, "purchases", "Purchases", 40),
        
        // Finance & Accounting group
        new("finance.budgets", "Budgets", "/finance/budgets", "bar-chart", CorePermissions.BudgetsRead, 5, "finance", "Finance & Accounting", 50),
        new("finance.journals", "Journals", "/finance/journals", "book", CorePermissions.JournalsRead, 10, "finance", "Finance & Accounting", 50),
        new("finance.cheques", "Cheque register", "/finance/cheques", "credit-card", CorePermissions.ChequesRead, 15, "finance", "Finance & Accounting", 50),
        new("finance.bank", "Cash and bank", "/finance/bank", "bank", CorePermissions.BankRead, 20, "finance", "Finance & Accounting", 50),
        new("core.reports", "Reports", "/reports", "bar-chart", CorePermissions.ReportsRead, 30, "finance", "Finance & Accounting", 50),
        new("finance.accounts", "Chart of accounts", "/finance/accounts", "account-book", CorePermissions.AccountsRead, 40, "finance", "Finance & Accounting", 50),
        new("finance.taxcodes", "Tax codes", "/finance/tax-codes", "percentage", CorePermissions.TaxRead, 50, "finance", "Finance & Accounting", 50),
        
        // Master Data group
        new("core.parties", "All parties", "/parties", "team", CorePermissions.PartiesRead, 10, "masterdata", "Master Data", 60),
        new("core.items", "Items", "/catalog/items", "shopping", CorePermissions.ItemsRead, 20, "masterdata", "Master Data", 60),
        
        // Setup group
        new("core.legalentities", "Legal entities", "/admin/legal-entities", "apartment", CorePermissions.EntitiesRead, 5, "setup", "Setup", 900),
        new("core.businessunits", "Business units", "/admin/business-units", "cluster", CorePermissions.BusinessUnitsRead, 10, "setup", "Setup", 900),
        new("core.users", "Users", "/admin/users", "user", CorePermissions.UsersRead, 20, "setup", "Setup", 900),
        new("core.roles", "Roles", "/admin/roles", "safety", CorePermissions.RolesRead, 30, "setup", "Setup", 900),
        new("core.customfields", "Custom fields", "/admin/custom-fields", "form", CorePermissions.CustomFieldsRead, 40, "setup", "Setup", 900),
        new("core.modules", "Modules", "/admin/modules", "appstore-add", CorePermissions.SettingsRead, 50, "setup", "Setup", 900),
        new("core.approvalrules", "Approval rules", "/admin/approval-rules", "safety", CorePermissions.SettingsRead, 60, "setup", "Setup", 900),
        new("core.settings", "Settings", "/admin/settings", "setting", CorePermissions.SettingsRead, 70, "setup", "Setup", 900),
        new("core.import", "Data import", "/admin/import", "download", CorePermissions.PartiesWrite, 80, "setup", "Setup", 900),
        new("core.documents", "Document vault", "/admin/documents", "book", CorePermissions.AttachmentsRead, 85, "setup", "Setup", 900),
        new("core.messages", "Message log", "/admin/messages", "mail", CorePermissions.SettingsRead, 90, "setup", "Setup", 900),
        new("core.audit", "Audit log", "/admin/audit", "audit", CorePermissions.AuditRead, 95, "setup", "Setup", 900),
    ];
}
