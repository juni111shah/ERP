using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class PayrollRunRepository(IRepository<PayrollRun> inner) : RepositoryBase<PayrollRun>(inner), IPayrollRunRepository
{
    public async Task<PayrollRun> GetWithLinesAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<PayrollRun>(r => r.Id == id).Include(r => r.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<PayrollRun> GetWithLinesForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<PayrollRun>(r => r.Id == id).Include(r => r.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<PayrollRun?> FindLiveFinalSettlementOfAsync(Guid employeeId, CancellationToken ct)
        => (await Query(new Specification<PayrollRun>(r => r.Kind == PayrollKind.FinalSettlement && r.Status != PayrollStatus.Voided).Include(r => r.Lines)).ToListAsync(ct)).FirstOrDefault(r => r.Lines.Any(l => l.EmployeeId == employeeId));

    public async Task<bool> IsOnLiveRegularRunAsync(Guid legalEntityId, int year, int month, Guid employeeId, CancellationToken ct)
        => (await Query(new Specification<PayrollRun>(r => r.LegalEntityId == legalEntityId && r.Year == year && r.Month == month && r.Kind == PayrollKind.Regular && r.Status != PayrollStatus.Voided).Include(r => r.Lines)).ToListAsync(ct))
            .Any(r => r.Lines.Any(l => l.EmployeeId == employeeId));

    public Task<List<PayrollRun>> ListLiveFinalSettlementsWithLinesAsync(Guid legalEntityId, int year, int month, CancellationToken ct)
        => Query(new Specification<PayrollRun>(r => r.LegalEntityId == legalEntityId && r.Year == year && r.Month == month && r.Kind == PayrollKind.FinalSettlement && r.Status != PayrollStatus.Voided).Include(r => r.Lines)).ToListAsync(ct);

    public Task<List<PayrollRun>> ListPostedOrPaidWithLinesUpToAsync(Guid legalEntityId, int year, int month, CancellationToken ct)
        => Query(new Specification<PayrollRun>(r => r.LegalEntityId == legalEntityId && r.Year == year && r.Month <= month && (r.Status == PayrollStatus.Posted || r.Status == PayrollStatus.Paid)).Include(r => r.Lines)).ToListAsync(ct);

    public Task<List<MyPayslipDto>> ListPayslipsOfAsync(Guid employeeId, Guid? lineId, CancellationToken ct)
        => Query(new Specification<PayrollRun>(r => r.Status == PayrollStatus.Posted || r.Status == PayrollStatus.Paid))
            .SelectMany(r => r.Lines.Where(l => l.EmployeeId == employeeId && (lineId == null || l.Id == lineId)), (r, l) => new MyPayslipDto(l.Id, r.Id, r.Number, r.Year, r.Month, r.Status, r.PaidOn, l.EmployeeName, l.EmployeeNumber,
                l.Basic, l.Allowances, l.Overtime, l.Absence, l.Adjustment, l.AdjustmentNote, l.AbsentDays, l.OvertimeHours, l.GrossEarnings, l.Deductions, l.NetPay))
            .ToListAsync(ct);

    public Task<List<PayrollRunTotals>> ListPostedTotalsAsync(Guid? legalEntityId, int fromMonthIndex, int toMonthIndex, CancellationToken ct)
        => Query(new Specification<PayrollRun>(r => (legalEntityId == null || r.LegalEntityId == legalEntityId) && (r.Status == PayrollStatus.Posted || r.Status == PayrollStatus.Paid) && r.Year * 12 + r.Month >= fromMonthIndex && r.Year * 12 + r.Month <= toMonthIndex))
            .Select(r => new PayrollRunTotals(r.Year, r.Month, r.Employees, r.GrossEarnings, r.Deductions, r.NetPay)).ToListAsync(ct);
}
