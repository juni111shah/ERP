using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for staff loans and salary advances.</summary>
public interface IEmployeeLoanRepository : IRepository<EmployeeLoan>
{
    /// <summary>What is left on each of an employee's active loans with a balance, and whether it was paid out.</summary>
    Task<List<OpenLoanBalance>> ListOpenBalancesAsync(Guid employeeId, CancellationToken ct);

    /// <summary>An employee's active, paid-out loans with a balance, oldest start first.</summary>
    Task<List<EmployeeLoan>> ListRecoverableOldestFirstAsync(Guid employeeId, CancellationToken ct);

    /// <summary>The active, paid-out loans with a balance of the entity's employees among <paramref name="employeeIds"/>.</summary>
    Task<List<EmployeeLoan>> ListRecoverableOfEntityAsync(Guid legalEntityId, List<Guid> employeeIds, CancellationToken ct);
}

/// <summary>What is left on one open loan, and when it was paid out (null: never).</summary>
public sealed record OpenLoanBalance(decimal Remaining, DateOnly? DisbursedOn);
