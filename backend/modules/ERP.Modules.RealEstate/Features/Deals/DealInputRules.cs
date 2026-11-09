using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

/// <summary>Checks what a deal form refers to and copies it onto the deal (shared by create and update).</summary>
internal sealed class DealInputRules(IReadRepository<Property> properties, IReadRepository<Unit> units, IReadRepository<Party> parties)
{
    public async Task ValidateReferencesAsync(DealInput i, CancellationToken ct)
    {
        if (i.PropertyId is { } propertyId && !await properties.AnyAsync(p => p.Id == propertyId, ct)) throw new FieldValidationException("propertyId", "Property not found.");
        if (i.UnitId is { } unitId && !await units.AnyAsync(u => u.Id == unitId && (i.PropertyId == null || u.PropertyId == i.PropertyId), ct))
            throw new FieldValidationException("unitId", "Unit not found in that property.");
        foreach (var (field, id) in new[] { ("clientPartyId", i.ClientPartyId), ("agentPartyId", i.AgentPartyId), ("teamLeadPartyId", i.TeamLeadPartyId) })
            if (id is { } partyId && !await parties.AnyAsync(p => p.Id == partyId, ct)) throw new FieldValidationException(field, "Party not found.");
    }

    public static void Map(DealInput i, BrokerageDeal d)
    {
        d.DealType = i.DealType; d.PropertyId = i.PropertyId; d.UnitId = i.UnitId; d.UnitReference = i.UnitReference; d.ClientPartyId = i.ClientPartyId;
        d.ClientName = i.ClientName.Trim(); d.AgentPartyId = i.AgentPartyId; d.ExternalAgentName = i.ExternalAgentName; d.TeamLeadPartyId = i.TeamLeadPartyId;
        d.DealDate = i.DealDate; d.DealValue = i.DealValue; d.CommissionRatePercent = i.CommissionRatePercent; d.TaxCodeId = i.TaxCodeId;
        d.AgentSharePercent = i.AgentSharePercent; d.TeamLeadSharePercent = i.TeamLeadSharePercent; d.Notes = i.Notes;
        d.ComputeCommission();
    }
}
