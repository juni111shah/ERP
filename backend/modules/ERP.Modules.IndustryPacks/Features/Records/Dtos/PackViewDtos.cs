using ERP.Modules.IndustryPacks.Domain;

namespace ERP.Modules.IndustryPacks.Features;

public sealed record RecordTypeView(
    string Key, string Label, string Singular, string Icon, IReadOnlyList<RecordFieldDef> Fields, IReadOnlyList<RecordStatusDef> Statuses, string? PartyRole, string? PartyLabel,
    string? ParentType, string? ParentLabel, bool HasLines, string? LinesLabel, bool HasAmount, bool HasDueDate, bool CanInvoice, bool CanBill, bool PerLineBilling, IReadOnlyList<string>? BillableStatuses,
    bool ConsumesStock, bool TracksProgress, bool Sensitive, string? TitleLabel);
public sealed record PackView(string Key, string Name, string Description, IReadOnlyList<RecordTypeView> Types);
public sealed record RecordStatusCount(string Status, int Count, decimal Amount);
public sealed record RecordTypeSummary(string Type, string Label, int Total, decimal Amount, IReadOnlyList<RecordStatusCount> ByStatus);
