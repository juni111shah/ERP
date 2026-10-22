namespace ERP.Application.Security;

/// <summary>A permission key in the form &lt;module&gt;.&lt;resource&gt;.&lt;action&gt; (e.g. core.users.write, finance.cheques.approve).</summary>
public sealed record PermissionDefinition(string Key, string Module, string Resource, string Action, string Description)
{
    public static PermissionDefinition Of(string key, string description)
    {
        var parts = key.Split('.');
        if (parts.Length != 3 || parts.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException($"Permission key '{key}' must be <module>.<resource>.<action>.");
        return new(key, parts[0], parts[1], parts[2], description);
    }
}

/// <summary>Core permission keys. Modules declare their own via IModule.Permissions.</summary>
public static class CorePermissions
{
    /// <summary>Granted to the built-in Org admin role: expands to every registered permission.</summary>
    public const string All = "*";

    public const string SettingsRead = "core.settings.read";
    public const string SettingsWrite = "core.settings.write";
    public const string EntitiesRead = "core.entities.read";
    public const string EntitiesWrite = "core.entities.write";
    public const string BusinessUnitsRead = "core.businessunits.read";
    public const string BusinessUnitsWrite = "core.businessunits.write";
    public const string UsersRead = "core.users.read";
    public const string UsersWrite = "core.users.write";
    public const string RolesRead = "core.roles.read";
    public const string RolesWrite = "core.roles.write";
    public const string ModulesManage = "core.modules.manage";
    public const string CustomFieldsRead = "core.customfields.read";
    public const string CustomFieldsManage = "core.customfields.manage";
    public const string AuditRead = "core.audit.read";
    public const string ApprovalsDecide = "core.approvals.decide";
    public const string SensitiveRead = "core.sensitive.read";
    public const string PartiesRead = "core.parties.read";
    public const string PartiesWrite = "core.parties.write";
    public const string ItemsRead = "core.items.read";
    public const string ItemsWrite = "core.items.write";
    public const string AttachmentsRead = "core.attachments.read";
    public const string AttachmentsWrite = "core.attachments.write";

    public const string AccountsRead = "finance.accounts.read";
    public const string AccountsWrite = "finance.accounts.write";
    public const string JournalsRead = "finance.journals.read";
    public const string JournalsWrite = "finance.journals.write";
    public const string JournalsPost = "finance.journals.post";
    public const string ChequesRead = "finance.cheques.read";
    public const string ChequesWrite = "finance.cheques.write";
    public const string ChequesApprove = "finance.cheques.approve";
    public const string TaxRead = "finance.tax.read";
    public const string TaxWrite = "finance.tax.write";
    public const string CurrenciesRead = "finance.currencies.read";
    public const string CurrenciesWrite = "finance.currencies.write";
    public const string ReportsRead = "finance.reports.read";
    public const string BankRead = "finance.bank.read";
    public const string BankWrite = "finance.bank.write";
    public const string BillsRead = "finance.bills.read";
    public const string BillsWrite = "finance.bills.write";
    public const string BillsPost = "finance.bills.post";
    public const string PaymentsRead = "finance.payments.read";
    public const string PaymentsWrite = "finance.payments.write";
    public const string InvoicesRead = "finance.invoices.read";
    public const string InvoicesWrite = "finance.invoices.write";
    public const string InvoicesIssue = "finance.invoices.issue";
    public const string ReceiptsRead = "finance.receipts.read";
    public const string ReceiptsWrite = "finance.receipts.write";

    public static IReadOnlyList<PermissionDefinition> Definitions { get; } =
    [
        PermissionDefinition.Of(SettingsRead, "View workspace settings"),
        PermissionDefinition.Of(SettingsWrite, "Change workspace settings"),
        PermissionDefinition.Of(EntitiesRead, "View legal entities"),
        PermissionDefinition.Of(EntitiesWrite, "Create and edit legal entities, lock periods"),
        PermissionDefinition.Of(BusinessUnitsRead, "View business units"),
        PermissionDefinition.Of(BusinessUnitsWrite, "Create and edit business units"),
        PermissionDefinition.Of(UsersRead, "View users"),
        PermissionDefinition.Of(UsersWrite, "Invite, edit and deactivate users"),
        PermissionDefinition.Of(RolesRead, "View roles"),
        PermissionDefinition.Of(RolesWrite, "Create and edit roles"),
        PermissionDefinition.Of(ModulesManage, "Enable and disable modules"),
        PermissionDefinition.Of(CustomFieldsRead, "View custom field definitions"),
        PermissionDefinition.Of(CustomFieldsManage, "Create and edit custom fields"),
        PermissionDefinition.Of(AuditRead, "View the audit log"),
        PermissionDefinition.Of(ApprovalsDecide, "Approve or reject requests made by others"),
        PermissionDefinition.Of(SensitiveRead, "See unmasked sensitive values (IDs, IBANs)"),
        PermissionDefinition.Of(PartiesRead, "View customers, vendors and other parties"),
        PermissionDefinition.Of(PartiesWrite, "Create and edit parties"),
        PermissionDefinition.Of(ItemsRead, "View products and services"),
        PermissionDefinition.Of(ItemsWrite, "Create and edit products and services"),
        PermissionDefinition.Of(AttachmentsRead, "View and download attachments"),
        PermissionDefinition.Of(AttachmentsWrite, "Upload and delete attachments"),
        PermissionDefinition.Of(AccountsRead, "View the chart of accounts"),
        PermissionDefinition.Of(AccountsWrite, "Edit the chart of accounts"),
        PermissionDefinition.Of(JournalsRead, "View journals"),
        PermissionDefinition.Of(JournalsWrite, "Create draft journals"),
        PermissionDefinition.Of(JournalsPost, "Post journals"),
        PermissionDefinition.Of(ChequesRead, "View cheques and post-dated cheques"),
        PermissionDefinition.Of(ChequesWrite, "Record and update cheques"),
        PermissionDefinition.Of(ChequesApprove, "Approve cheque actions (deposit, bounce, replace)"),
        PermissionDefinition.Of(TaxRead, "View tax codes"),
        PermissionDefinition.Of(TaxWrite, "Edit tax codes"),
        PermissionDefinition.Of(CurrenciesRead, "View currencies and exchange rates"),
        PermissionDefinition.Of(CurrenciesWrite, "Edit exchange rates"),
        PermissionDefinition.Of(ReportsRead, "View financial reports"),
        PermissionDefinition.Of(BankRead, "View bank balances and reconciliations"),
        PermissionDefinition.Of(BankWrite, "Import statements, reconcile and transfer funds"),
        PermissionDefinition.Of(BillsRead, "View vendor bills"),
        PermissionDefinition.Of(BillsWrite, "Create and edit draft vendor bills"),
        PermissionDefinition.Of(BillsPost, "Post and void vendor bills"),
        PermissionDefinition.Of(PaymentsRead, "View vendor payments"),
        PermissionDefinition.Of(PaymentsWrite, "Make, allocate and void vendor payments"),
        PermissionDefinition.Of(InvoicesRead, "View invoices and credit notes"),
        PermissionDefinition.Of(InvoicesWrite, "Create and edit draft invoices and credit notes"),
        PermissionDefinition.Of(InvoicesIssue, "Issue, void and credit invoices"),
        PermissionDefinition.Of(ReceiptsRead, "View customer receipts"),
        PermissionDefinition.Of(ReceiptsWrite, "Record, allocate and void customer receipts"),
    ];
}

/// <summary>All permissions known to this deployment (core + installed modules).</summary>
public interface IPermissionCatalog
{
    IReadOnlyList<PermissionDefinition> All { get; }
    /// <summary>Expands "*" to every key and drops unknown keys.</summary>
    IReadOnlySet<string> Expand(IEnumerable<string> granted);
}

/// <summary>Marks a MediatR request that needs a permission (defence in depth on top of endpoint policies).</summary>
public interface IRequirePermission
{
    string RequiredPermission { get; }
}
