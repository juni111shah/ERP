using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetJobOpeningHandler(IReadRepository<JobOpening> openings, ICandidateRepository candidates) : IQueryHandler<GetJobOpeningQuery, JobOpeningDto>
{
    public async Task<JobOpeningDto> Handle(GetJobOpeningQuery query, CancellationToken ct)
    {
        var o = await openings.GetAsync(query.Id, ct);
        return JobOpeningDto.From(o, await candidates.ListStagesOfOpeningAsync(o.Id, ct));
    }
}
