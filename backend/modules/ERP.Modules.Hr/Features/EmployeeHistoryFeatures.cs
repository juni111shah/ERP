using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Reminders;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record HistoryNoteInput(string Summary, DateOnly? EffectiveDate = null);
/// <summary>Old and new values of pay entries are null for callers without hr.salary.read.</summary>
public sealed record EmployeeHistoryDto(Guid Id, DateOnly EffectiveDate, HistoryKind Kind, string Summary, string? OldValue, string? NewValue, bool IsPay, Guid? ChangedByUserId);

public sealed record GetEmployeeHistory(Guid EmployeeId) : IQuery<IReadOnlyList<EmployeeHistoryDto>>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
public sealed record AddEmployeeNote(Guid EmployeeId, HistoryNoteInput Input) : ICommand<EmployeeHistoryDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }

public sealed class AddEmployeeNoteValidator : AbstractValidator<AddEmployeeNote>
{
    public AddEmployeeNoteValidator() { RuleFor(x => x.Input.Summary).NotEmpty().MaximumLength(500); }
}

/// <summary>The values of an employee that are tracked for history.</summary>
public sealed record EmployeeSnapshot(decimal Basic, decimal Gross, string Designation, string? Department, Guid? DepartmentId, Guid? PositionId, Guid? ManagerId, ContractType? Contract, EmployeeStatus Status)
{
    public static EmployeeSnapshot Of(Employee e) => new(e.BasicSalary, e.GrossMonthly, e.Designation, e.Department, e.DepartmentId, e.PositionId, e.ManagerEmployeeId, e.ContractType, e.Status);
}

public sealed record HistoryChange(HistoryKind Kind, string Summary, string? Old, string? New, bool IsPay);

/// <summary>What changed between two snapshots; no I/O.</summary>
public static class HistoryMath
{
    public static IReadOnlyList<HistoryChange> Diff(EmployeeSnapshot before, EmployeeSnapshot after)
    {
        var list = new List<HistoryChange>();
        if (before.Basic != after.Basic)
            list.Add(new HistoryChange(HistoryKind.Salary, after.Basic > before.Basic ? "Basic salary increased" : "Basic salary reduced", before.Basic.ToString("0.00"), after.Basic.ToString("0.00"), true));
        else if (before.Gross != after.Gross)
            list.Add(new HistoryChange(HistoryKind.Salary, "Allowances changed", before.Gross.ToString("0.00"), after.Gross.ToString("0.00"), true));
        if (!string.Equals(before.Designation, after.Designation, StringComparison.Ordinal))
            list.Add(new HistoryChange(HistoryKind.Designation, "Designation changed", before.Designation, after.Designation, false));
        if (before.DepartmentId != after.DepartmentId || !string.Equals(before.Department, after.Department, StringComparison.Ordinal))
            list.Add(new HistoryChange(HistoryKind.Department, "Department changed", before.Department, after.Department, false));
        if (before.PositionId != after.PositionId) list.Add(new HistoryChange(HistoryKind.Position, "Position changed", before.PositionId?.ToString(), after.PositionId?.ToString(), false));
        if (before.ManagerId != after.ManagerId) list.Add(new HistoryChange(HistoryKind.Manager, "Line manager changed", before.ManagerId?.ToString(), after.ManagerId?.ToString(), false));
        if (before.Contract != after.Contract) list.Add(new HistoryChange(HistoryKind.Contract, "Contract type changed", before.Contract?.ToString(), after.Contract?.ToString(), false));
        if (before.Status != after.Status && after.Status != EmployeeStatus.Terminated) list.Add(new HistoryChange(HistoryKind.Status, "Status changed", before.Status.ToString(), after.Status.ToString(), false));
        return list;
    }
}

/// <summary>Writes history entries; called from the employee handlers.</summary>
public sealed class EmployeeHistoryRecorder(IRepository<EmployeeHistoryEntry> entries, IClock clock, ICurrentUser user)
{
    public DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public void Add(Employee e, HistoryKind kind, string summary, string? oldValue, string? newValue, bool isPay, DateOnly? date = null)
        => entries.Add(new EmployeeHistoryEntry { LegalEntityId = e.LegalEntityId, EmployeeId = e.Id, EffectiveDate = date ?? Today, Kind = kind, Summary = summary, OldValue = oldValue, NewValue = newValue, IsPay = isPay, ChangedByUserId = user.UserId });

    public void Joined(Employee e) => Add(e, HistoryKind.Joined, "Joined", null, e.Designation, false, e.JoinDate);

    public void Changed(Employee e, EmployeeSnapshot before)
    {
        foreach (var c in HistoryMath.Diff(before, EmployeeSnapshot.Of(e))) Add(e, c.Kind, c.Summary, c.Old, c.New, c.IsPay);
    }

    public void Terminated(Employee e, DateOnly date, string? reason) => Add(e, HistoryKind.Terminated, string.IsNullOrWhiteSpace(reason) ? "Employment ended" : $"Employment ended: {reason.Trim()}", null, null, false, date);
}

internal sealed class EmployeeHistoryHandlers(IRepository<EmployeeHistoryEntry> entries, IReadRepository<Employee> employees, EmployeeHistoryRecorder recorder, ICurrentUser user) :
    IRequestHandler<GetEmployeeHistory, IReadOnlyList<EmployeeHistoryDto>>,
    IRequestHandler<AddEmployeeNote, EmployeeHistoryDto>
{
    public async Task<IReadOnlyList<EmployeeHistoryDto>> Handle(GetEmployeeHistory q, CancellationToken ct)
    {
        var canSeePay = user.HasPermission(P.SalaryRead);
        var rows = await entries.Query(new Specification<EmployeeHistoryEntry>(h => h.EmployeeId == q.EmployeeId)).ToListAsync(ct);
        return rows.OrderByDescending(h => h.EffectiveDate).ThenByDescending(h => h.CreatedAt).Select(h => ToDto(h, canSeePay)).ToList();
    }

    public async Task<EmployeeHistoryDto> Handle(AddEmployeeNote cmd, CancellationToken ct)
    {
        var e = await employees.FindAsync(cmd.EmployeeId, ct) ?? throw new NotFoundException();
        recorder.Add(e, HistoryKind.Note, cmd.Input.Summary.Trim(), null, null, false, cmd.Input.EffectiveDate);
        await entries.SaveChangesAsync(ct);
        var last = (await entries.Query(new Specification<EmployeeHistoryEntry>(h => h.EmployeeId == e.Id && h.Kind == HistoryKind.Note)).ToListAsync(ct)).OrderByDescending(h => h.CreatedAt).First();
        return ToDto(last, user.HasPermission(P.SalaryRead));
    }

    private static EmployeeHistoryDto ToDto(EmployeeHistoryEntry h, bool canSeePay)
        => new(h.Id, h.EffectiveDate, h.Kind, h.Summary, h.IsPay && !canSeePay ? null : h.OldValue, h.IsPay && !canSeePay ? null : h.NewValue, h.IsPay, h.ChangedByUserId);
}

/// <summary>Reminds HR managers when a probation period or fixed-term contract is about to end (30 and 7 days before). Reads the data directly: jobs run without a signed-in user.</summary>
public sealed class EmployeeContractReminders(IReadRepository<Employee> employees) : IReminderSource
{
    public string? ModuleKey => HrModule.Key;
    public static readonly int[] Stages = [30, 7];

    public static int? StageFor(int daysLeft) => daysLeft < 0 ? null : Stages.Where(s => daysLeft <= s).OrderBy(s => s).Cast<int?>().FirstOrDefault();

    public async Task<IReadOnlyList<ReminderCandidate>> GetDueAsync(DateOnly today, CancellationToken ct)
    {
        var limit = today.AddDays(Stages.Max());
        var staff = await employees.Query(new Specification<Employee>(e => e.Status != EmployeeStatus.Terminated
            && ((e.ProbationEndDate != null && e.ProbationEndDate >= today && e.ProbationEndDate <= limit) || (e.ContractEndDate != null && e.ContractEndDate >= today && e.ContractEndDate <= limit)))).ToListAsync(ct);
        var list = new List<ReminderCandidate>();
        foreach (var e in staff)
        {
            foreach (var (label, date) in new[] { ("Probation", e.ProbationEndDate), ("Contract", e.ContractEndDate) })
            {
                if (date is not { } d || d < today || d > limit) continue;
                var left = d.DayNumber - today.DayNumber;
                if (StageFor(left) is not { } stage) continue;
                list.Add(new ReminderCandidate($"hr-contract:{e.Id}:{label}:{d:yyyyMMdd}:{stage}", $"{label} of {e.FullName} ends in {left} day(s)",
                    $"{label} of {e.Number} {e.FullName} ends on {d:dd MMM yyyy}.", "/hr/employees", P.EmployeesManage));
            }
        }
        return list;
    }
}
