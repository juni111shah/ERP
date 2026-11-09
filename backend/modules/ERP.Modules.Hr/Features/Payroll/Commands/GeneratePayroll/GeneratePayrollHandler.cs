using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GeneratePayrollHandler(
    IPayrollRunRepository runs, IPayrollLoanRecoveryRepository recoveries, IPayrollLineStatutoryRepository statutoryRows, PayrollLineBuilder builder,
    ILegalEntityGuard guard, INumberingService numbering, IUnitOfWork uow, PayrollSeparation separation) : ICommandHandler<GeneratePayrollCommand, PayrollDto>
{
    public Task<PayrollDto> Handle(GeneratePayrollCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. One live regular run per entity and month.
        var i = command.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, token);
        if (i.Kind == PayrollKind.Regular && await runs.AnyAsync(r => r.LegalEntityId == entity.Id && r.Year == i.Year && r.Month == i.Month && r.Kind == PayrollKind.Regular && r.Status != PayrollStatus.Voided, token))
            throw new ConflictException("There is already a payroll run for this entity and month. Void it first, or create an off-cycle run.");

        // 2. The numbered draft with its lines.
        var run = new PayrollRun { LegalEntityId = entity.Id, Year = i.Year, Month = i.Month, PlannedPayDate = i.PlannedPayDate, Notes = i.Notes?.Trim(), Status = PayrollStatus.Draft, Kind = i.Kind, IncludeSalary = i.Kind == PayrollKind.Regular || i.IncludeSalary };
        separation.StampGenerated(run);
        run.Number = await numbering.NextAsync(Docs.Payroll, entity.Id, new DateOnly(i.Year, i.Month, 1), token);
        var (lines, recs, stats) = await builder.BuildLinesAsync(run, new Dictionary<Guid, (decimal, string?)>(), i.Kind == PayrollKind.OffCycle && i.EmployeeIds is { Count: > 0 } only ? only.ToHashSet() : null, token);
        if (lines.Count == 0) throw new ConflictException("No active employees are on the payroll for this month.");
        run.Lines.AddRange(lines); PayrollRunRules.Totals(run);

        // 3. Add the run, its loan recoveries and statutory rows, and save.
        runs.Add(run);
        recoveries.AddRange(recs.Select(r => { r.PayrollRunId = run.Id; return r; }));
        statutoryRows.AddRange(stats);
        await runs.SaveChangesAsync(token);
        return PayrollDto.From(run);
    }, ct);
}
