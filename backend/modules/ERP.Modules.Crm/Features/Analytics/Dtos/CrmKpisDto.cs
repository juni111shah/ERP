namespace ERP.Modules.Crm.Features;

public record CrmKpisDto(
    int TotalLeads,
    int QualifiedLeads,
    decimal LeadConversionRatePercentage,
    int OpenOpportunitiesCount,
    decimal OpenPipelineValue,
    int WonOpportunitiesCount,
    decimal WonOpportunitiesRevenue,
    decimal WinRatePercentage);
