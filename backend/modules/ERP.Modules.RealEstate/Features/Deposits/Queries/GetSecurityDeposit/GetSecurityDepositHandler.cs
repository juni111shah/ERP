using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetSecurityDepositHandler(IReadRepository<Lease> leases, SecurityDepositLedger ledger) : IQueryHandler<GetSecurityDepositQuery, DepositDto>
{
    public async Task<DepositDto> Handle(GetSecurityDepositQuery q, CancellationToken ct) => await ledger.BuildAsync(await leases.GetAsync(q.LeaseId, ct), ct);
}
