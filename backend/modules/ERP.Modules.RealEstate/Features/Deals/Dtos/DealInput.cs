using System.Text.Json.Nodes;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record DealInput(
    Guid LegalEntityId, DealType DealType, string ClientName, DateOnly DealDate, decimal DealValue, decimal CommissionRatePercent,
    Guid? PropertyId = null, Guid? UnitId = null, string? UnitReference = null, Guid? ClientPartyId = null, Guid? AgentPartyId = null,
    string? ExternalAgentName = null, Guid? TeamLeadPartyId = null, Guid? TaxCodeId = null, decimal AgentSharePercent = 0,
    decimal TeamLeadSharePercent = 0, string? Notes = null, JsonObject? CustomFields = null);
