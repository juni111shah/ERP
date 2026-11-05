using ERP.Domain.Common;

namespace ERP.Modules.Projects.Domain;

public enum ExpenseStatus { Draft = 0, Submitted = 1, Approved = 2, Rejected = 3 }
public enum ChangeOrderStatus { Draft = 0, Approved = 1, Rejected = 2 }
public enum RiskKind { Risk = 0, Issue = 1 }
public enum RiskSeverity { Low = 0, Medium = 1, High = 2, Critical = 3 }
public enum RiskStatus { Open = 0, Mitigated = 1, Closed = 2 }

/// <summary>What an hour costs and what it is billed at. Most specific match wins: project + person, person, project + role, project, workspace default.</summary>
public class ProjectRate : OrgEntity
{
    [Filterable] public Guid? ProjectId { get; set; }
    [Filterable] public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public decimal BillRate { get; set; }
    public decimal CostRate { get; set; }
}

/// <summary>Money spent for a project (travel, materials, subcontractor ...), optionally charged to the customer with a mark-up.</summary>
[DefaultSort("-date")]
public class ProjectExpense : OrgEntity
{
    [Filterable] public Guid ProjectId { get; set; }
    [Sortable, Filterable] public DateOnly Date { get; set; }
    [Filterable] public string Category { get; set; } = string.Empty;
    [Searchable] public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsBillable { get; set; } = true;
    public decimal MarkupPercent { get; set; }
    [Filterable] public Guid? WorkerUserId { get; set; }
    [Searchable] public string? WorkerName { get; set; }
    [Filterable, Sortable] public ExpenseStatus Status { get; set; } = ExpenseStatus.Approved;
    public string? RejectReason { get; set; }
    [Filterable] public bool IsBilled { get; set; }
    public Guid? InvoiceId { get; set; }
    [Filterable] public bool WrittenOff { get; set; }
    public Guid? CostJournalId { get; set; }
    public Guid? WipReleaseJournalId { get; set; }
}

public class ProjectTaskDependency : OrgEntity
{
    [Filterable] public Guid TaskId { get; set; }
    public Guid DependsOnTaskId { get; set; }
}

[DefaultSort("-createdAt")]
public class ProjectComment : OrgEntity
{
    /// <summary>"project" or "task".</summary>
    [Filterable] public string OwnerType { get; set; } = "project";
    [Filterable] public Guid OwnerId { get; set; }
    [Filterable] public Guid ProjectId { get; set; }
    public Guid? UserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    [Searchable] public string Text { get; set; } = string.Empty;
}

[DefaultSort("-createdAt")]
public class ProjectChangeOrder : OrgEntity
{
    [Filterable] public Guid ProjectId { get; set; }
    [Searchable, Sortable] public string Number { get; set; } = string.Empty;
    [Searchable] public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Added to the project budget when approved (negative = less).</summary>
    public decimal AmountChange { get; set; }
    public decimal HoursChange { get; set; }
    [Filterable, Sortable] public ChangeOrderStatus Status { get; set; } = ChangeOrderStatus.Draft;
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
}

[DefaultSort("-createdAt")]
public class ProjectRisk : OrgEntity
{
    [Filterable] public Guid ProjectId { get; set; }
    [Filterable] public RiskKind Kind { get; set; }
    [Searchable] public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    [Filterable, Sortable] public RiskSeverity Severity { get; set; } = RiskSeverity.Medium;
    [Filterable, Sortable] public RiskStatus Status { get; set; } = RiskStatus.Open;
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    [Sortable] public DateOnly? DueDate { get; set; }
}

public enum AcceptanceStatus { Pending = 0, Signed = 1, Declined = 2 }

/// <summary>A request for the customer to sign off the project or one of its milestones; answered in the customer portal with a typed signature.</summary>
[DefaultSort("-requestedAt")]
public class ProjectAcceptance : OrgEntity
{
    [Filterable] public Guid ProjectId { get; set; }
    public Guid? MilestoneId { get; set; }
    [Searchable] public string Title { get; set; } = string.Empty;
    [Filterable, Sortable] public AcceptanceStatus Status { get; set; } = AcceptanceStatus.Pending;
    public DateTime RequestedAt { get; set; }
    public string? RequestedByName { get; set; }
    public string? SignedByName { get; set; }
    public string? Signature { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? Comment { get; set; }
}

/// <summary>A person on the project team.</summary>
public class ProjectMember : OrgEntity
{
    [Filterable] public Guid ProjectId { get; set; }
    [Filterable] public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
}

/// <summary>Planned effort of a person on a project for a period; compared with capacity and logged time in the utilisation report.</summary>
public class ProjectAllocation : OrgEntity
{
    [Filterable] public Guid ProjectId { get; set; }
    [Filterable] public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    [Sortable] public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public decimal HoursPerWeek { get; set; }
}

/// <summary>A percentage of a fixed-fee project invoiced without a milestone. The percentages of live rows never pass 100.</summary>
[DefaultSort("-date")]
public class ProjectProgressBilling : OrgEntity
{
    [Filterable] public Guid ProjectId { get; set; }
    [Sortable] public DateOnly Date { get; set; }
    public decimal Percent { get; set; }
    public decimal Amount { get; set; }
    public Guid? InvoiceId { get; set; }
    /// <summary>Set when the invoice was voided; the percentage is free to bill again.</summary>
    [Filterable] public bool Released { get; set; }
}
