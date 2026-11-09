using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for the statutory amounts of payroll lines.</summary>
public interface IPayrollLineStatutoryRepository : IRepository<PayrollLineStatutory>
{
    /// <summary>Per rule of a run: how many employees, the base and the amount, by rule code.</summary>
    Task<List<StatutoryTotalDto>> TotalsForRunAsync(Guid runId, CancellationToken ct);

    /// <summary>Every statutory row of a run, tracked for changes.</summary>
    Task<List<PayrollLineStatutory>> ListForRunForUpdateAsync(Guid runId, CancellationToken ct);
}
