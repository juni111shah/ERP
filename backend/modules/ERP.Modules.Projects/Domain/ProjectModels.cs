using ERP.Domain.Common;

namespace ERP.Modules.Projects.Domain;

public enum ProjectStatus
{
    Planning = 0,
    Active = 1,
    OnHold = 2,
    Completed = 3,
    Cancelled = 4,
}

public enum ProjectBillingType
{
    TimeAndMaterials = 0,
    FixedFee = 1,
    NonBillable = 2,
}

public enum MilestoneStatus
{
    Pending = 0,
    Achieved = 1,
    Invoiced = 2,
    Cancelled = 3,
}

public enum ProjectTaskStatus
{
    Todo = 0,
    InProgress = 1,
    Review = 2,
    Done = 3,
    Blocked = 4,
    Cancelled = 5,
}

public enum TimesheetStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
}

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3,
}

public class Project : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Number { get; set; } = string.Empty;

    [Searchable, Sortable]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Filterable]
    public Guid? CustomerId { get; set; }

    [Searchable]
    public string? CustomerName { get; set; }

    [Searchable]
    public string? ManagerName { get; set; }

    [Filterable, Sortable]
    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

    [Filterable, Sortable]
    public ProjectBillingType BillingType { get; set; } = ProjectBillingType.TimeAndMaterials;

    [Sortable]
    public decimal BudgetAmount { get; set; }

    [Sortable]
    public DateOnly? StartDate { get; set; }

    [Sortable]
    public DateOnly? EndDate { get; set; }

    public string? CustomFields { get; set; }

    /// <summary>The project manager's sign-in account (the typed ManagerName stays as the display name).</summary>
    [Filterable]
    public Guid? ManagerUserId { get; set; }

    /// <summary>Tax code for invoices of this project; blank = the workspace default.</summary>
    public Guid? TaxCodeId { get; set; }

    /// <summary>Budgeted hours (0 = not set).</summary>
    public decimal BudgetHours { get; set; }

    /// <summary>A template is a project kept only to be cloned; it takes no time and no billing.</summary>
    [Filterable]
    public bool IsTemplate { get; set; }

    [Filterable]
    public bool IsArchived { get; set; }

    /// <summary>Where the project came from (for example "sales.order", "crm.opportunity", "realestate.property") and the id there.</summary>
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }

    public ICollection<ProjectMilestone> Milestones { get; set; } = new List<ProjectMilestone>();
    public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
    public ICollection<ProjectTimesheet> Timesheets { get; set; } = new List<ProjectTimesheet>();
}

public class ProjectMilestone : OrgEntity
{
    [Filterable]
    public Guid ProjectId { get; set; }

    [Searchable, Sortable]
    public string Title { get; set; } = string.Empty;

    [Sortable]
    public DateOnly DueDate { get; set; }

    [Sortable]
    public decimal Amount { get; set; }

    [Filterable, Sortable]
    public MilestoneStatus Status { get; set; } = MilestoneStatus.Pending;

    [Sortable]
    public DateOnly? AchievedDate { get; set; }

    /// <summary>The invoice that billed this milestone (so voiding it can release the milestone).</summary>
    public Guid? InvoiceId { get; set; }
}

public class ProjectTask : OrgEntity
{
    [Filterable]
    public Guid ProjectId { get; set; }

    [Filterable]
    public Guid? MilestoneId { get; set; }

    [Searchable, Sortable]
    public string Number { get; set; } = string.Empty;

    [Searchable, Sortable]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Filterable, Sortable]
    public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.Todo;

    [Filterable, Sortable]
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    [Searchable, Filterable]
    public string? AssignedTo { get; set; }

    /// <summary>The assignee's sign-in account (AssignedTo stays as the display name).</summary>
    [Filterable]
    public Guid? AssignedUserId { get; set; }

    [Filterable]
    public Guid? ParentTaskId { get; set; }

    [Sortable]
    public DateOnly? StartDate { get; set; }

    /// <summary>Comma-separated tags.</summary>
    public string? Labels { get; set; }

    public decimal EstimatedHours { get; set; }
    public decimal ActualHours { get; set; }

    [Sortable]
    public DateOnly? DueDate { get; set; }
}

public class ProjectTimesheet : OrgEntity
{
    [Filterable]
    public Guid ProjectId { get; set; }

    [Filterable]
    public Guid? TaskId { get; set; }

    [Sortable, Filterable]
    public DateOnly Date { get; set; }

    [Searchable, Filterable]
    public string WorkerName { get; set; } = string.Empty;

    [Sortable]
    public decimal Hours { get; set; }

    public decimal HourlyRate { get; set; }

    [Sortable]
    public decimal TotalAmount { get; set; }

    public string? Description { get; set; }

    [Filterable]
    public bool IsBillable { get; set; } = true;

    [Filterable]
    public bool IsBilled { get; set; } = false;

    [Filterable]
    public Guid? WorkerUserId { get; set; }

    /// <summary>What an hour of this person costs the company (the HourlyRate is what the customer pays).</summary>
    public decimal CostRate { get; set; }
    public decimal CostAmount { get; set; }

    [Filterable, Sortable]
    public TimesheetStatus ApprovalStatus { get; set; } = TimesheetStatus.Approved;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public string? RejectReason { get; set; }

    /// <summary>The journal that moved this entry's cost into work in progress.</summary>
    public Guid? CostJournalId { get; set; }

    /// <summary>Unbilled time the company decided not to charge for.</summary>
    [Filterable]
    public bool WrittenOff { get; set; }

    public Guid? InvoiceId { get; set; }
}
