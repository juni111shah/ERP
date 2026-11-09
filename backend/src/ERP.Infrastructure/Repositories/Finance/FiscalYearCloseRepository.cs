using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class FiscalYearCloseRepository(IRepository<FiscalYearClose> closes) : RepositoryBase<FiscalYearClose>(closes), IFiscalYearCloseRepository
{
    public Task<DateOnly?> GetLatestClosedYearEndAsync(Guid legalEntityId, CancellationToken ct)
        => Query(new Specification<FiscalYearClose>(c => c.LegalEntityId == legalEntityId && c.ReopenedAt == null)).OrderByDescending(c => c.YearEnd).Select(c => (DateOnly?)c.YearEnd).FirstOrDefaultAsync(ct);

    public Task<List<Guid>> ListEntitiesClosedByAsync(DateOnly asOf, Guid? entityId, CancellationToken ct)
        => Query(new Specification<FiscalYearClose>(c => c.ReopenedAt == null && c.YearEnd <= asOf && (entityId == null || c.LegalEntityId == entityId)))
            .Select(c => c.LegalEntityId).Distinct().ToListAsync(ct);

    public Task<FiscalYearClose?> FindLiveAsync(Guid legalEntityId, int fiscalYear, CancellationToken ct)
        => Query(Live(legalEntityId, fiscalYear)).FirstOrDefaultAsync(ct);

    public Task<FiscalYearClose?> FindLiveForUpdateAsync(Guid legalEntityId, int fiscalYear, CancellationToken ct)
        => Tracked(Live(legalEntityId, fiscalYear)).FirstOrDefaultAsync(ct);

    private static Specification<FiscalYearClose> Live(Guid legalEntityId, int fiscalYear)
        => new(c => c.LegalEntityId == legalEntityId && c.FiscalYear == fiscalYear && c.ReopenedAt == null);
}
