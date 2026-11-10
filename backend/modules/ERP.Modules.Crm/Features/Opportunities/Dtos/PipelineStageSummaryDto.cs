using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Features;

public record PipelineStageSummaryDto(
    OpportunityStage Stage,
    string StageName,
    int Count,
    decimal TotalExpectedRevenue);
