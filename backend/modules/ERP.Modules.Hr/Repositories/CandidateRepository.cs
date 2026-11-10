using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class CandidateRepository(IRepository<Candidate> inner) : RepositoryBase<Candidate>(inner), ICandidateRepository
{
    public Task<List<CandidateStage>> ListStagesOfOpeningAsync(Guid openingId, CancellationToken ct)
        => Query(new Specification<Candidate>(c => c.OpeningId == openingId)).Select(c => c.Stage).ToListAsync(ct);
}
