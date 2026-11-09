using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class UpdateListingHandler(IRepository<Listing> listings, ListingInputRules inputs) : ICommandHandler<UpdateListingCommand, ListingDto>
{
    public async Task<ListingDto> Handle(UpdateListingCommand cmd, CancellationToken ct)
    {
        var l = await listings.GetForUpdateAsync(cmd.Id, ct);
        if (l.Status == ListingStatus.Closed) throw new ConflictException("A closed listing can no longer be edited. Reopen it as a draft first.");
        if (cmd.Input.PropertyId != l.PropertyId) throw new FieldValidationException("propertyId", "A listing cannot move to another property.");
        await inputs.MapAsync(cmd.Input, l, ct);
        await listings.SaveChangesAsync(ct);
        return ListingDto.From(l);
    }
}
