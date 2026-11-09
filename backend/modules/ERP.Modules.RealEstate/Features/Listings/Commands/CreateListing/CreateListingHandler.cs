using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreateListingHandler(IRepository<Listing> listings, ListingInputRules inputs) : ICommandHandler<CreateListingCommand, ListingDto>
{
    public async Task<ListingDto> Handle(CreateListingCommand cmd, CancellationToken ct)
    {
        var l = new Listing();
        await inputs.MapAsync(cmd.Input, l, ct);
        listings.Add(l);
        await listings.SaveChangesAsync(ct);
        return ListingDto.From(l);
    }
}
