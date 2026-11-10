using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetOwnerPayoutHandler(IReadRepository<OwnerPayout> payouts) : IQueryHandler<GetOwnerPayoutQuery, OwnerPayoutDto>
{
    public async Task<OwnerPayoutDto> Handle(GetOwnerPayoutQuery q, CancellationToken ct) => OwnerPayoutDto.From(await payouts.GetAsync(q.Id, ct));
}
