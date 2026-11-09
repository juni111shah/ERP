using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

/// <summary>Checks what a lead form refers to and copies it onto the lead (shared by create and update).</summary>
internal sealed class LeadInputRules(IReadRepository<Listing> listings, IReadRepository<Unit> units, IReadRepository<Party> parties)
{
    public async Task MapAsync(LeadInput i, Lead l, CancellationToken ct)
    {
        if (i.AgentPartyId is { } agent && !await parties.AnyAsync(p => p.Id == agent, ct)) throw new FieldValidationException("agentPartyId", "Agent not found.");
        if (i.ListingId is { } lid && !await listings.AnyAsync(x => x.Id == lid, ct)) throw new FieldValidationException("listingId", "Listing not found.");
        if (i.UnitId is { } uid && !await units.AnyAsync(u => u.Id == uid, ct)) throw new FieldValidationException("unitId", "Unit not found.");
        l.Name = i.Name.Trim(); l.Phone = i.Phone?.Trim(); l.Email = i.Email?.Trim(); l.Source = i.Source; l.Interest = i.Interest;
        l.AgentPartyId = i.AgentPartyId; l.ListingId = i.ListingId; l.UnitId = i.UnitId; l.Notes = i.Notes; l.FollowUpOn = i.FollowUpOn;
    }
}
