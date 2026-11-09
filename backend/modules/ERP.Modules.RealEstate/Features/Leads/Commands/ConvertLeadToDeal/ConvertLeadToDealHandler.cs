using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using MediatR;

namespace ERP.Modules.RealEstate.Features;

internal sealed class ConvertLeadToDealHandler(IRepository<Lead> leads, IReadRepository<Listing> listings, ISender sender, IClock clock) : ICommandHandler<ConvertLeadToDealCommand, LeadDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<LeadDto> Handle(ConvertLeadToDealCommand cmd, CancellationToken ct)
    {
        // 1. Only an open lead can be converted.
        var l = await leads.GetForUpdateAsync(cmd.Id, ct);
        if (l.Stage == LeadStage.Won) throw new ConflictException("This lead was already converted.");
        if (l.Stage == LeadStage.Lost) throw new ConflictException("Reopen the lost lead before converting it.");

        // 2. Type, value and rate default from the lead's listing.
        var i = cmd.Input;
        var listing = l.ListingId is { } lid ? await listings.FindAsync(lid, ct) : null;
        var kind = i.DealType ?? (listing?.Kind == ListingKind.Sale ? LeadConvertTo.Sale : LeadConvertTo.Lease);
        var value = i.DealValue ?? listing?.Price ?? throw new FieldValidationException("dealValue", "Enter the deal value (the lead has no listing to take it from).");
        var rate = i.CommissionRatePercent ?? (kind == LeadConvertTo.Sale ? 2m : 5m);

        // 3. Create the deal, then mark the lead won.
        var deal = await sender.Send(new CreateDealCommand(new DealInput(l.LegalEntityId, kind == LeadConvertTo.Sale ? DealType.Sale : DealType.Lease, l.Name, Today, value, rate,
            listing?.PropertyId, l.UnitId ?? listing?.UnitId, null, i.ClientPartyId, l.AgentPartyId, Notes: $"Converted from lead. {l.Interest}".Trim())), ct);
        l.Stage = LeadStage.Won; l.LostReason = null; l.DealId = deal.Id;
        await leads.SaveChangesAsync(ct);
        return LeadDto.From(l);
    }
}
