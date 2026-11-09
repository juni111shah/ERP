using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateFinalSettlementHandler(
    IReadRepository<Employee> employees, IPayrollRunRepository runs, IEmployeeLoanRepository loans, IRepository<PayrollLoanRecovery> recoveries, IRepository<PayrollLineStatutory> statutoryRows,
    ILegalEntityGuard guard, INumberingService numbering, PayrollSeparation separation, IUnitOfWork uow, SettlementCalculator settlement) : ICommandHandler<CreateFinalSettlementCommand, PayrollDto>
{
    public Task<PayrollDto> Handle(CreateFinalSettlementCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Only a terminated employee without a live settlement.
        var e = await employees.FindAsync(command.EmployeeId, token) ?? throw new NotFoundException();
        if (e.Status != EmployeeStatus.Terminated || e.TerminationDate is not { } term) throw new ConflictException("Terminate the employee before preparing the final settlement.");
        var entity = await guard.RequireAsync(e.LegalEntityId, token);
        var existing = await settlement.ExistingSettlementAsync(e.Id, token);
        if (existing is not null) throw new ConflictException("This employee already has a final settlement. Void it first to prepare a new one.");
        var p = await settlement.BuildAsync(e, term, e.LastWorkingDay, e.NoticeGivenOn, token);

        // 2. A one-line run from the preview.
        var run = new PayrollRun { LegalEntityId = entity.Id, Year = p.Year, Month = p.Month, Status = PayrollStatus.Draft, Kind = PayrollKind.FinalSettlement, IncludeSalary = true, Notes = $"Final settlement: {e.FullName}" };
        separation.StampGenerated(run);
        run.Number = await numbering.NextAsync(Docs.Payroll, entity.Id, new DateOnly(p.Year, p.Month, 1), token);
        var line = new PayrollLine
        {
            PayrollRunId = run.Id, EmployeeId = e.Id, EmployeeName = e.FullName, EmployeeNumber = e.Number, ProratedDays = PayrollMath.EmployedDays(e.JoinDate, e.TerminationDate, p.Year, p.Month),
            Basic = p.FinalBasic, Allowances = p.FinalAllowances, LeaveEncashment = p.LeaveEncashment, EndOfService = p.EndOfService, LoanRecovery = p.LoanRecovery,
            GrossEarnings = p.Gross, Deductions = p.Deductions, NetPay = p.NetPay, StatutoryDeduction = p.StatutoryDeduction, EmployerCost = p.EmployerCost,
            AdjustmentNote = p.NoticeShortfallDeduction > 0 ? $"Includes notice shortfall of {p.NoticeShortfallDays:0} days" : null,
        };
        run.Lines.Add(line);
        run.Employees = 1; run.GrossEarnings = line.GrossEarnings; run.Deductions = line.Deductions; run.NetPay = line.NetPay; run.EmployerCost = line.EmployerCost;
        runs.Add(run);
        foreach (var r in await settlement.StatutoryForAsync(e, p.FinalBasic, p.FinalAllowances, p.Year, p.Month, token))
            statutoryRows.Add(new PayrollLineStatutory { PayrollRunId = run.Id, PayrollLineId = line.Id, EmployeeId = e.Id, RuleId = r.RuleId, Code = r.Code, Name = r.Name, Kind = r.Kind, BaseAmount = r.BaseAmount, Amount = r.Amount });

        // 3. Spread the loan recovery over the open loans, oldest first, and save.
        var left = p.LoanRecovery;
        foreach (var loan in await loans.ListRecoverableOldestFirstAsync(e.Id, token))
        {
            if (left <= 0) break;
            var take = Math.Min(left, loan.Remaining);
            recoveries.Add(new PayrollLoanRecovery { PayrollRunId = run.Id, PayrollLineId = line.Id, LoanId = loan.Id, Amount = take });
            left -= take;
        }
        await runs.SaveChangesAsync(token);
        return PayrollDto.From(run);
    }, ct);
}
