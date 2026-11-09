using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class GetClientHandler(ClientReader clients) : IQueryHandler<GetClientQuery, ClientDetail>
{
    public Task<ClientDetail> Handle(GetClientQuery query, CancellationToken ct) => clients.DetailAsync(query.OrgId, ct);
}
