using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetListingHandler(IReadRepository<Listing> listings) : IQueryHandler<GetListingQuery, ListingDto>
{
    public async Task<ListingDto> Handle(GetListingQuery q, CancellationToken ct) => ListingDto.From(await listings.GetAsync(q.Id, ct));
}
