using ERP.Domain.Common;

namespace ERP.Modules.Crm.Domain;

public enum LeadStatus
{
    New = 1,
    Contacted = 2,
    Qualified = 3,
    Unqualified = 4,
    Converted = 5
}

public enum LeadSource
{
    Website = 1,
    Referral = 2,
    TradeShow = 3,
    ColdCall = 4,
    Campaign = 5,
    Other = 99
}

public enum OpportunityStage
{
    Qualification = 1,
    Proposal = 2,
    Negotiation = 3,
    ClosedWon = 4,
    ClosedLost = 5
}

public enum InteractionType
{
    Call = 1,
    Meeting = 2,
    Email = 3,
    Note = 4,
    Task = 5
}

[DefaultSort("-leadNumber")]
public class CrmLead : LegalEntityScopedEntity
{
    [Searchable, Sortable]
    public string LeadNumber { get; set; } = string.Empty;
    [Searchable, Sortable]
    public string FullName { get; set; } = string.Empty;
    [Searchable, Sortable]
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    [Searchable]
    public string? Email { get; set; }
    [Searchable]
    public string? Phone { get; set; }
    [Sortable, Filterable]
    public LeadSource Source { get; set; } = LeadSource.Website;
    [Sortable, Filterable]
    public LeadStatus Status { get; set; } = LeadStatus.New;
    [Sortable]
    public decimal EstimatedValue { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public string? Notes { get; set; }
    public Guid? ConvertedPartyId { get; set; }
    public Guid? ConvertedOpportunityId { get; set; }
    public DateTimeOffset? ConvertedAt { get; set; }
    public string? CustomFields { get; set; }
}

[DefaultSort("-opportunityNumber")]
public class CrmOpportunity : LegalEntityScopedEntity
{
    [Searchable, Sortable]
    public string OpportunityNumber { get; set; } = string.Empty;
    [Searchable, Sortable]
    public string Title { get; set; } = string.Empty;
    public Guid? CustomerPartyId { get; set; }
    [Searchable, Sortable]
    public string CustomerName { get; set; } = string.Empty;
    [Sortable, Filterable]
    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;
    [Sortable]
    public int ProbabilityPercent { get; set; } = 20;
    [Sortable]
    public decimal ExpectedRevenue { get; set; }
    public decimal ActualRevenue { get; set; }
    [Sortable]
    public DateOnly? ExpectedCloseDate { get; set; }
    public DateOnly? ActualCloseDate { get; set; }
    public string? CloseReason { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public string? Notes { get; set; }
    public string? CustomFields { get; set; }
}

[DefaultSort("-createdAt")]
public class CrmInteraction : LegalEntityScopedEntity
{
    public string RelatedEntityKind { get; set; } = "Lead"; // Lead, Opportunity, Customer
    public Guid RelatedEntityId { get; set; }
    [Sortable, Filterable]
    public InteractionType Type { get; set; } = InteractionType.Call;
    [Searchable, Sortable]
    public string Subject { get; set; } = string.Empty;
    public string? Details { get; set; }
    [Sortable]
    public DateTimeOffset InteractionDate { get; set; } = DateTimeOffset.UtcNow;
    public Guid? PerformedByUserId { get; set; }
    public string? PerformedByName { get; set; }
    [Sortable]
    public DateTimeOffset? FollowUpDate { get; set; }
    public bool IsResolved { get; set; } = true;
    public string? CustomFields { get; set; }
}
