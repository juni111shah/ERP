using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Features;

public record OpportunityDto(
    Guid Id,
    Guid LegalEntityId,
    string OpportunityNumber,
    string Title,
    Guid? CustomerPartyId,
    string CustomerName,
    OpportunityStage Stage,
    int ProbabilityPercent,
    decimal ExpectedRevenue,
    decimal ActualRevenue,
    DateOnly? ExpectedCloseDate,
    DateOnly? ActualCloseDate,
    string? CloseReason,
    Guid? LeadId,
    Guid? AssignedUserId,
    string? AssignedUserName,
    string? Notes,
    string? CustomFields,
    DateTimeOffset CreatedAt)
{
    public static OpportunityDto From(CrmOpportunity opp) => new(
        opp.Id,
        opp.LegalEntityId,
        opp.OpportunityNumber,
        opp.Title,
        opp.CustomerPartyId,
        opp.CustomerName,
        opp.Stage,
        opp.ProbabilityPercent,
        opp.ExpectedRevenue,
        opp.ActualRevenue,
        opp.ExpectedCloseDate,
        opp.ActualCloseDate,
        opp.CloseReason,
        opp.LeadId,
        opp.AssignedUserId,
        opp.AssignedUserName,
        opp.Notes,
        opp.CustomFields,
        opp.CreatedAt);
}
