using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class ReviewCycleRepository(IRepository<ReviewCycle> inner) : RepositoryBase<ReviewCycle>(inner), IReviewCycleRepository
{
    public async Task<string> GetNameAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<ReviewCycle>(c => c.Id == id)).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "";
}
