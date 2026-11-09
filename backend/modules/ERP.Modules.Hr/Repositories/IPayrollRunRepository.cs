using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for payroll runs and their lines.</summary>
public interface IPayrollRunRepository : IRepository<PayrollRun>
{
    /// <summary>The run with its lines, read-only. Throws NotFoundException.</summary>
    Task<PayrollRun> GetWithLinesAsync(Guid id, CancellationToken ct);

    /// <summary>The run with its lines, tracked for changes. Throws NotFoundException.</summary>
    Task<PayrollRun> GetWithLinesForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The final settlement (not voided) that holds a line of the employee; null when there is none.</summary>
    Task<PayrollRun?> FindLiveFinalSettlementOfAsync(Guid employeeId, CancellationToken ct);

    /// <summary>True when a regular run (not voided) of the entity and month has a line of the employee.</summary>
    Task<bool> IsOnLiveRegularRunAsync(Guid legalEntityId, int year, int month, Guid employeeId, CancellationToken ct);

    /// <summary>The final settlements (not voided) of the entity and month, with their lines.</summary>
    Task<List<PayrollRun>> ListLiveFinalSettlementsWithLinesAsync(Guid legalEntityId, int year, int month, CancellationToken ct);

    /// <summary>The posted and paid runs of the entity from January up to <paramref name="month"/> of the year, with their lines (year to date).</summary>
    Task<List<PayrollRun>> ListPostedOrPaidWithLinesUpToAsync(Guid legalEntityId, int year, int month, CancellationToken ct);

    /// <summary>An employee's payslips of posted and paid runs (only line <paramref name="lineId"/> when given).</summary>
    Task<List<MyPayslipDto>> ListPayslipsOfAsync(Guid employeeId, Guid? lineId, CancellationToken ct);

    /// <summary>The totals of the posted and paid runs (of one entity, or all) whose month index (year x 12 + month) is from <paramref name="fromMonthIndex"/> to <paramref name="toMonthIndex"/>.</summary>
    Task<List<PayrollRunTotals>> ListPostedTotalsAsync(Guid? legalEntityId, int fromMonthIndex, int toMonthIndex, CancellationToken ct);
}

/// <summary>The month and totals of one payroll run.</summary>
public sealed record PayrollRunTotals(int Year, int Month, int Employees, decimal GrossEarnings, decimal Deductions, decimal NetPay);
