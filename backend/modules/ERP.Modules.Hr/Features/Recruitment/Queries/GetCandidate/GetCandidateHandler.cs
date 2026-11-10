using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class GetCandidateHandler(IReadRepository<Candidate> candidates) : IQueryHandler<GetCandidateQuery, CandidateDto>
{
    public async Task<CandidateDto> Handle(GetCandidateQuery query, CancellationToken ct) => CandidateDto.From(await candidates.GetAsync(query.Id, ct));
}
