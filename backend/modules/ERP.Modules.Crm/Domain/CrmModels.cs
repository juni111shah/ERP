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

public class CrmLead : LegalEntityScopedEntity
{
    public string LeadNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public LeadSource Source { get; set; } = LeadSource.Website;
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public decimal EstimatedValue { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public string? Notes { get; set; }
    public Guid? ConvertedPartyId { get; set; }
    public Guid? ConvertedOpportunityId { get; set; }
    public DateTimeOffset? ConvertedAt { get; set; }
    public string? CustomFields { get; set; }
}

public class CrmOpportunity : LegalEntityScopedEntity
{
    public string OpportunityNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid? CustomerPartyId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;
    public int ProbabilityPercent { get; set; } = 20;
    public decimal ExpectedRevenue { get; set; }
    public decimal ActualRevenue { get; set; }
    public DateOnly? ExpectedCloseDate { get; set; }
    public DateOnly? ActualCloseDate { get; set; }
    public string? CloseReason { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public string? Notes { get; set; }
    public string? CustomFields { get; set; }
}

public class CrmInteraction : LegalEntityScopedEntity
{
    public string RelatedEntityKind { get; set; } = "Lead"; // Lead, Opportunity, Customer
    public Guid RelatedEntityId { get; set; }
    public InteractionType Type { get; set; } = InteractionType.Call;
    public string Subject { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTimeOffset InteractionDate { get; set; } = DateTimeOffset.UtcNow;
    public Guid? PerformedByUserId { get; set; }
    public string? PerformedByName { get; set; }
    public DateTimeOffset? FollowUpDate { get; set; }
    public bool IsResolved { get; set; } = true;
    public string? CustomFields { get; set; }
}
