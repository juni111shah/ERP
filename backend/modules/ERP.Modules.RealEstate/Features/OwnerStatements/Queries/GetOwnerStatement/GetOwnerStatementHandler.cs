using ERP.Application.Common;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetOwnerStatementHandler(OwnerStatementBuilder builder) : IQueryHandler<GetOwnerStatementQuery, OwnerStatementDto>
{
    public Task<OwnerStatementDto> Handle(GetOwnerStatementQuery q, CancellationToken ct) => builder.BuildAsync(q.OwnerPartyId, q.From, q.To, null, false, ct);
}
