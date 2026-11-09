using System.Text.Json.Nodes;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record DealDto(
    Guid Id, string? Number, Guid LegalEntityId, DealType DealType, Guid? PropertyId, Guid? UnitId, string? UnitReference, Guid? ClientPartyId, string ClientName,
    Guid? AgentPartyId, string? ExternalAgentName, Guid? TeamLeadPartyId, DateOnly DealDate, DealStage Stage, decimal DealValue, string CurrencyCode,
    decimal CommissionRatePercent, Guid? TaxCodeId, decimal AgentSharePercent, decimal TeamLeadSharePercent, decimal GrossCommission, decimal AgentCommission,
    decimal TeamLeadCommission, string? LostReason, string? Notes, JsonObject CustomFields, Guid? CommissionInvoiceId = null, DateOnly? AgentPaidOn = null)
{
    public static DealDto From(BrokerageDeal d, JsonObject customFields) => new(
        d.Id, d.Number, d.LegalEntityId, d.DealType, d.PropertyId, d.UnitId, d.UnitReference, d.ClientPartyId, d.ClientName, d.AgentPartyId, d.ExternalAgentName,
        d.TeamLeadPartyId, d.DealDate, d.Stage, d.DealValue, d.CurrencyCode, d.CommissionRatePercent, d.TaxCodeId, d.AgentSharePercent, d.TeamLeadSharePercent,
        d.GrossCommission, Share(d.GrossCommission, d.AgentSharePercent), Share(d.GrossCommission, d.TeamLeadSharePercent), d.LostReason, d.Notes, customFields, d.CommissionInvoiceId, d.AgentPaidOn);

    private static decimal Share(decimal gross, decimal percent) => Math.Round(gross * percent / 100m, 2, MidpointRounding.AwayFromZero);
}
