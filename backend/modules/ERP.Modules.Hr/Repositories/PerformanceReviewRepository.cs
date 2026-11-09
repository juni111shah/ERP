using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class PerformanceReviewRepository(IRepository<PerformanceReview> inner) : RepositoryBase<PerformanceReview>(inner), IPerformanceReviewRepository
{
    public async Task<PerformanceReview> GetWithCompetenciesAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<PerformanceReview>(r => r.Id == id).Include(r => r.Competencies)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<PerformanceReview> GetWithCompetenciesForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<PerformanceReview>(r => r.Id == id).Include(r => r.Competencies)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
}
