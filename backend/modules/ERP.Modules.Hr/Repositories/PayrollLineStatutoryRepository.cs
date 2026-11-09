using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class PayrollLineStatutoryRepository(IRepository<PayrollLineStatutory> inner) : RepositoryBase<PayrollLineStatutory>(inner), IPayrollLineStatutoryRepository
{
    public async Task<List<StatutoryTotalDto>> TotalsForRunAsync(Guid runId, CancellationToken ct)
        => (await Query(new Specification<PayrollLineStatutory>(l => l.PayrollRunId == runId)).ToListAsync(ct))
            .GroupBy(l => (l.Code, l.Name, l.Kind)).OrderBy(g => g.Key.Code)
            .Select(g => new StatutoryTotalDto(g.Key.Code, g.Key.Name, g.Key.Kind, g.Select(x => x.EmployeeId).Distinct().Count(), g.Sum(x => x.BaseAmount), g.Sum(x => x.Amount))).ToList();

    public Task<List<PayrollLineStatutory>> ListForRunForUpdateAsync(Guid runId, CancellationToken ct)
        => Tracked(new Specification<PayrollLineStatutory>(x => x.PayrollRunId == runId)).ToListAsync(ct);
}
