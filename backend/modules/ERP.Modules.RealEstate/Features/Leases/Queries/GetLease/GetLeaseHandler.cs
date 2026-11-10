using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetLeaseHandler(IReadRepository<Lease> leases, LeaseDtoFactory factory) : IQueryHandler<GetLeaseQuery, LeaseDto>
{
    public async Task<LeaseDto> Handle(GetLeaseQuery q, CancellationToken ct) => await factory.BuildAsync(await leases.GetAsync(q.Id, ct), ct);
}
