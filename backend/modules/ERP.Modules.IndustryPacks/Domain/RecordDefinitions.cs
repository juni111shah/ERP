namespace ERP.Modules.IndustryPacks.Domain;

public sealed record RecordFieldDef(
    string Key, string Label, string Type = "text", bool Required = false, IReadOnlyList<string>? Options = null,
    bool Sensitive = false, bool InList = false, string? Help = null);

public sealed record RecordStatusDef(string Key, string Label, string Tone = "default");

/// <summary><c>Approval</c> needs the type's <c>.approve</c> permission and a different person from the one who created the record.</summary>
public sealed record RecordTransitionDef(string From, string To, string Label, bool ReasonRequired = false, bool Approval = false);

/// <summary>How a record turns into a core finance document. The ledger is only ever written through the core documents.</summary>
public sealed record RecordBillingDef(
    bool Invoice = false, bool Bill = false, bool PerLine = false, IReadOnlyList<string>? WhenStatus = null,
    string? ExpenseAccountRole = null, string? AfterBillingStatus = null);

/// <summary>A dashboard card computed from real records of this type.</summary>
public sealed record RecordKpiDef(string Key, string Label, IReadOnlyList<string>? Statuses = null, bool SumAmount = false, string? Caption = null, bool OverdueOnly = false);

/// <summary>Everything the engine needs to serve a record type: fields, statuses, rules, billing and stock behaviour.</summary>
public sealed record RecordTypeDef(
    string Key, string Label, string Singular, string Icon, string Prefix,
    IReadOnlyList<RecordFieldDef> Fields, IReadOnlyList<RecordStatusDef> Statuses, IReadOnlyList<RecordTransitionDef> Transitions,
    string? PartyRole = null, string? PartyLabel = null, string? ParentType = null, string? ParentLabel = null,
    bool HasLines = false, string? LinesLabel = null, bool HasAmount = false, bool HasDueDate = false,
    RecordBillingDef? Billing = null, bool ConsumesStock = false, string? ConsumeAccountRole = null, bool TracksProgress = false,
    bool Sensitive = false, IReadOnlyList<RecordKpiDef>? Kpis = null, int MenuOrder = 10, string? TitleLabel = null)
{
    public string InitialStatus => Statuses[0].Key;
    public RecordFieldDef? Field(string key) => Fields.FirstOrDefault(f => f.Key == key);
}

/// <summary>Short forms for declaring record types, so a pack reads as a business description.</summary>
public static class Def
{
    public static RecordFieldDef Text(string key, string label, bool required = false, bool inList = false, bool sensitive = false, string? help = null) => new(key, label, "text", required, null, sensitive, inList, help);
    public static RecordFieldDef LongText(string key, string label, bool required = false, bool sensitive = false) => new(key, label, "textarea", required, null, sensitive);
    public static RecordFieldDef Number(string key, string label, bool required = false, bool inList = false, string? help = null) => new(key, label, "number", required, null, false, inList, help);
    public static RecordFieldDef Money(string key, string label, bool required = false, bool inList = false) => new(key, label, "money", required, null, false, inList);
    public static RecordFieldDef Date(string key, string label, bool required = false, bool inList = false, bool sensitive = false) => new(key, label, "date", required, null, sensitive, inList);
    public static RecordFieldDef Flag(string key, string label, bool inList = false) => new(key, label, "bool", false, null, false, inList);
    public static RecordFieldDef Choice(string key, string label, string options, bool required = false, bool inList = false, bool sensitive = false)
        => new(key, label, "select", required, options.Split('|'), sensitive, inList);

    public static RecordStatusDef S(string key, string label, string tone = "default") => new(key, label, tone);
    public static RecordTransitionDef T(string from, string to, string label, bool reason = false, bool approval = false) => new(from, to, label, reason, approval);

    /// <summary>The common active/inactive master-data life cycle.</summary>
    public static readonly RecordStatusDef[] MasterStatuses = [S("active", "Active", "success"), S("inactive", "Inactive")];
    public static readonly RecordTransitionDef[] MasterTransitions = [T("active", "inactive", "Deactivate", reason: true), T("inactive", "active", "Reactivate")];

    /// <summary>The common draft, approval, completion life cycle, with rejection and cancellation.</summary>
    public static readonly RecordStatusDef[] DocumentStatuses =
    [
        S("draft", "Draft"), S("submitted", "Submitted", "processing"), S("approved", "Approved", "success"),
        S("rejected", "Rejected", "error"), S("completed", "Completed", "success"), S("cancelled", "Cancelled", "error"),
    ];
    public static readonly RecordTransitionDef[] DocumentTransitions =
    [
        T("draft", "submitted", "Submit"), T("draft", "cancelled", "Cancel", reason: true),
        T("submitted", "approved", "Approve", approval: true), T("submitted", "rejected", "Reject", reason: true, approval: true),
        T("rejected", "draft", "Reopen"), T("approved", "completed", "Complete"), T("approved", "cancelled", "Cancel", reason: true),
    ];
}
