using ERP.Domain.Common;

namespace ERP.Modules.Services.Domain;

public enum EngagementType
{
    FixedPrice = 1,
    TimeAndMaterials = 2,
    Retainer = 3
}

public enum EngagementStatus
{
    Draft = 1,
    Active = 2,
    OnHold = 3,
    Completed = 4,
    Cancelled = 5
}

public enum RetainerCycle
{
    Monthly = 1,
    Quarterly = 2,
    Annual = 3
}

public enum RetainerStatus
{
    Active = 1,
    Paused = 2,
    Terminated = 3
}

public enum MilestoneStatus
{
    Planned = 1,
    ReadyToInvoice = 2,
    Invoiced = 3,
    Paid = 4
}

[DefaultSort("-engagementCode")]
public class ServiceEngagement : LegalEntityScopedEntity
{
    [Searchable, Sortable]
    public string EngagementCode { get; set; } = string.Empty;
    [Searchable, Sortable]
    public string Title { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    [Searchable, Sortable]
    public string ClientName { get; set; } = string.Empty;
    [Sortable, Filterable]
    public EngagementType Type { get; set; } = EngagementType.FixedPrice;
    [Sortable, Filterable]
    public EngagementStatus Status { get; set; } = EngagementStatus.Draft;
    [Sortable]
    public decimal ContractValue { get; set; }
    public decimal BudgetedHours { get; set; }
    public decimal ConsumedHours { get; set; }
    public decimal ConsumedCost { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? TargetEndDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public Guid? LeadConsultantUserId { get; set; }
    public string? LeadConsultantName { get; set; }
    public string? Description { get; set; }
    public string? CustomFields { get; set; }
}

[DefaultSort("-retainerNumber")]
public class RetainerContract : LegalEntityScopedEntity
{
    [Searchable, Sortable]
    public string RetainerNumber { get; set; } = string.Empty;
    public Guid EngagementId { get; set; }
    public Guid ClientId { get; set; }
    [Searchable, Sortable]
    public string ClientName { get; set; } = string.Empty;
    [Searchable, Sortable]
    public string Title { get; set; } = string.Empty;
    [Sortable, Filterable]
    public RetainerCycle Cycle { get; set; } = RetainerCycle.Monthly;
    [Sortable, Filterable]
    public RetainerStatus Status { get; set; } = RetainerStatus.Active;
    [Sortable]
    public decimal RecurringFee { get; set; }
    public decimal IncludedHours { get; set; }
    public decimal OverageHourlyRate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Terms { get; set; }
    public string? CustomFields { get; set; }
}

[DefaultSort("dueDate")]
public class BillingMilestone : LegalEntityScopedEntity
{
    [Searchable, Sortable]
    public string MilestoneCode { get; set; } = string.Empty;
    [Filterable]
    public Guid EngagementId { get; set; }
    [Searchable, Sortable]
    public string Title { get; set; } = string.Empty;
    [Sortable]
    public decimal Amount { get; set; }
    [Sortable]
    public DateOnly DueDate { get; set; }
    public DateOnly? CompletionDate { get; set; }
    [Sortable, Filterable]
    public MilestoneStatus Status { get; set; } = MilestoneStatus.Planned;
    public Guid? InvoiceId { get; set; }
    public string? Notes { get; set; }
    public string? CustomFields { get; set; }
}

public class TimeEntryRecord : LegalEntityScopedEntity
{
    public Guid EngagementId { get; set; }
    public Guid ConsultantUserId { get; set; }
    public string ConsultantName { get; set; } = string.Empty;
    public DateOnly ActivityDate { get; set; }
    public decimal Hours { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal TotalAmount { get; set; }
    public string TaskDescription { get; set; } = string.Empty;
    public bool IsBillable { get; set; } = true;
    public bool IsApproved { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? CustomFields { get; set; }
}
