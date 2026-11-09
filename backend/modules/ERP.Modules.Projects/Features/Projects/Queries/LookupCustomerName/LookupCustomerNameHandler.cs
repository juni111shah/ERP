using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Modules.Projects.Features;

internal sealed class LookupCustomerNameHandler(IReadRepository<ERP.Domain.Parties.Party> parties) : IQueryHandler<LookupCustomerNameQuery, string?>
{
    public async Task<string?> Handle(LookupCustomerNameQuery query, CancellationToken ct) => (await parties.FindAsync(query.PartyId, ct))?.Name;
}
