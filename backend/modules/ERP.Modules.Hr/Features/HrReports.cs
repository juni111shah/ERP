using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record HrCount(string Key, int Count);
public sealed record HrMonthMovement(int Year, int Month, int Joiners, int Leavers);
public sealed record HrMonthCost(int Year, int Month, int Runs, int Employees, decimal Gross, decimal Deductions, decimal Net);
public sealed record HrLeaveByType(string LeaveType, decimal Days);
public sealed record HrMonthAbsence(int Year, int Month, decimal Days);

/// <summary>
/// <c>PayrollCost</c> is null when the caller lacks <c>hr.payroll.read</c>. Headcount and service years are as at <c>To</c>; movements, cost, leave and absence cover <c>From</c> to <c>To</c>.
/// </summary>
public sealed record HrSummaryReportDto(
    DateOnly From, DateOnly To, Guid? LegalEntityId, int Headcount, decimal AverageServiceYears,
    IReadOnlyList<HrCount> ByDepartment, IReadOnlyList<HrCount> ByNationality, IReadOnlyList<HrCount> ByContractType,
    IReadOnlyList<HrMonthMovement> Movements, IReadOnlyList<HrMonthCost>? PayrollCost,
    IReadOnlyList<HrLeaveByType> LeaveDaysByType, decimal AbsenceDays, IReadOnlyList<HrMonthAbsence> AbsenceByMonth);

public sealed record HrOccasion(Guid EmployeeId, string Number, string Employee, DateOnly Date, int DaysAway, int Years);
public sealed record HrOccasionsDto(DateOnly AsOf, int Days, IReadOnlyList<HrOccasion> Birthdays, IReadOnlyList<HrOccasion> Anniversaries);

public sealed record GetHrSummaryReport(DateOnly? From, DateOnly? To, Guid? LegalEntityId) : IQuery<HrSummaryReportDto>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
public sealed record GetBirthdaysAnniversaries(int Days, Guid? LegalEntityId) : IQuery<HrOccasionsDto>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }

public sealed class GetHrSummaryReportValidator : AbstractValidator<GetHrSummaryReport>
{
    public GetHrSummaryReportValidator()
    {
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To >= x.From).WithName("to").WithMessage("The end date cannot be before the start date.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To.Value.DayNumber - x.From.Value.DayNumber <= 366 * 5).WithName("to").WithMessage("Choose a period of at most five years.");
    }
}

public sealed class GetBirthdaysAnniversariesValidator : AbstractValidator<GetBirthdaysAnniversaries>
{
    public GetBirthdaysAnniversariesValidator() => RuleFor(x => x.Days).InclusiveBetween(1, 366);
}

/// <summary>
/// Custom-field keys the reports read until employee master data has first-class fields for them: <c>cf_contract_type</c> (text) and <c>cf_date_of_birth</c> (yyyy-MM-dd).
/// </summary>
internal static class EmployeeReportFields
{
    public const string ContractType = "cf_contract_type";
    public const string DateOfBirth = "cf_date_of_birth";

    /// <summary>The real date-of-birth field, or the custom field some workspaces used before it existed.</summary>
    public static DateOnly? BirthDate(DateOnly? real, string? customFields)
        => real ?? (DateOnly.TryParse(Text(customFields, DateOfBirth), out var parsed) ? parsed : null);

    public static string? Text(string? customFields, string key)
    {
        if (customFields is not { Length: > 0 } json) return null;
        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)?[key];
            return node is null ? null : (node.GetValueKind() == System.Text.Json.JsonValueKind.String ? node.GetValue<string>() : node.ToString())?.Trim() is { Length: > 0 } v ? v : null;
        }
        catch { return null; }
    }
}

internal sealed class HrReportHandlers(
    IReadRepository<Employee> employees, IReadRepository<PayrollRun> runs, IReadRepository<LeaveRequest> leave, IReadRepository<LeaveType> leaveTypes, IReadRepository<AttendanceRecord> attendance,
    ICurrentUser user, IClock clock) :
    IRequestHandler<GetHrSummaryReport, HrSummaryReportDto>,
    IRequestHandler<GetBirthdaysAnniversaries, HrOccasionsDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<HrSummaryReportDto> Handle(GetHrSummaryReport q, CancellationToken ct)
    {
        var to = q.To ?? Today;
        var from = q.From ?? new DateOnly(to.Year, 1, 1);
        var entity = q.LegalEntityId;
        var everyone = await employees.Query(new Specification<Employee>(e => entity == null || e.LegalEntityId == entity))
            .Select(e => new { e.Id, e.Department, e.Nationality, e.JoinDate, e.TerminationDate, e.Status, e.ContractType, e.CustomFields }).ToListAsync(ct);

        var current = everyone.Where(e => e.JoinDate <= to && (e.TerminationDate is null || e.TerminationDate > to) && !(e.Status == EmployeeStatus.Terminated && e.TerminationDate is null)).ToList();
        static string Label(string? v) => string.IsNullOrWhiteSpace(v) ? "Unspecified" : v.Trim();
        static List<HrCount> Count(IEnumerable<string> keys) => keys.GroupBy(k => k, StringComparer.OrdinalIgnoreCase).Select(g => new HrCount(g.First(), g.Count())).OrderByDescending(c => c.Count).ThenBy(c => c.Key).ToList();

        var months = Months(from, to);
        var movements = months.Select(m => new HrMonthMovement(m.Year, m.Month,
            everyone.Count(e => e.JoinDate.Year == m.Year && e.JoinDate.Month == m.Month),
            everyone.Count(e => e.TerminationDate is { } t && t.Year == m.Year && t.Month == m.Month))).ToList();
        var avg = current.Count == 0 ? 0m : Math.Round(current.Average(e => (decimal)(to.DayNumber - e.JoinDate.DayNumber) / 365.25m), 2);

        List<HrMonthCost>? cost = null;
        if (user.HasPermission(P.PayrollRead))
        {
            var lo = from.Year * 12 + from.Month; var hi = to.Year * 12 + to.Month;
            var posted = (await runs.Query(new Specification<PayrollRun>(r => (entity == null || r.LegalEntityId == entity) && (r.Status == PayrollStatus.Posted || r.Status == PayrollStatus.Paid) && r.Year * 12 + r.Month >= lo && r.Year * 12 + r.Month <= hi))
                .Select(r => new { r.Year, r.Month, r.Employees, r.GrossEarnings, r.Deductions, r.NetPay }).ToListAsync(ct));
            cost = months.Select(m =>
            {
                var rows = posted.Where(r => r.Year == m.Year && r.Month == m.Month).ToList();
                return new HrMonthCost(m.Year, m.Month, rows.Count, rows.Sum(r => r.Employees), rows.Sum(r => r.GrossEarnings), rows.Sum(r => r.Deductions), rows.Sum(r => r.NetPay));
            }).ToList();
        }

        var approved = await leave.Query(new Specification<LeaveRequest>(r => (entity == null || r.LegalEntityId == entity) && r.Status == LeaveStatus.Approved && r.FromDate >= from && r.FromDate <= to))
            .Select(r => new { r.LeaveTypeId, r.Days }).ToListAsync(ct);
        var typeNames = (await leaveTypes.Query().Select(t => new { t.Id, t.Name }).ToListAsync(ct)).ToDictionary(t => t.Id, t => t.Name);
        var leaveByType = approved.GroupBy(r => r.LeaveTypeId).Select(g => new HrLeaveByType(typeNames.GetValueOrDefault(g.Key, "Unknown"), g.Sum(r => r.Days))).OrderByDescending(x => x.Days).ThenBy(x => x.LeaveType).ToList();

        var lo2 = from.Year * 12 + from.Month; var hi2 = to.Year * 12 + to.Month;
        var attended = await attendance.Query(new Specification<AttendanceRecord>(r => (entity == null || r.LegalEntityId == entity) && r.Year * 12 + r.Month >= lo2 && r.Year * 12 + r.Month <= hi2 && r.UnpaidAbsentDays > 0))
            .Select(r => new { r.Year, r.Month, r.UnpaidAbsentDays }).ToListAsync(ct);
        var absence = months.Select(m => new HrMonthAbsence(m.Year, m.Month, attended.Where(a => a.Year == m.Year && a.Month == m.Month).Sum(a => a.UnpaidAbsentDays))).ToList();

        return new HrSummaryReportDto(from, to, entity, current.Count, avg,
            Count(current.Select(e => Label(e.Department))), Count(current.Select(e => Label(e.Nationality))),
            Count(current.Select(e => Label(e.ContractType?.ToString() ?? EmployeeReportFields.Text(e.CustomFields, EmployeeReportFields.ContractType)))),
            movements, cost, leaveByType, absence.Sum(a => a.Days), absence);
    }

    public async Task<HrOccasionsDto> Handle(GetBirthdaysAnniversaries q, CancellationToken ct)
    {
        var today = Today;
        var entity = q.LegalEntityId;
        var staff = await employees.Query(new Specification<Employee>(e => (entity == null || e.LegalEntityId == entity) && e.Status != EmployeeStatus.Terminated))
            .Select(e => new { e.Id, e.Number, e.FullName, e.JoinDate, e.DateOfBirth, e.CustomFields }).ToListAsync(ct);
        var birthdays = new List<HrOccasion>(); var anniversaries = new List<HrOccasion>();
        foreach (var e in staff.OrderBy(e => e.Number))
        {
            if (EmployeeReportFields.BirthDate(e.DateOfBirth, e.CustomFields) is { } born && Next(born, today) is var b && b.DayNumber - today.DayNumber <= q.Days)
                birthdays.Add(new HrOccasion(e.Id, e.Number, e.FullName, b, b.DayNumber - today.DayNumber, b.Year - born.Year));
            var a = Next(e.JoinDate, today);
            if (a.Year - e.JoinDate.Year >= 1 && a.DayNumber - today.DayNumber <= q.Days)
                anniversaries.Add(new HrOccasion(e.Id, e.Number, e.FullName, a, a.DayNumber - today.DayNumber, a.Year - e.JoinDate.Year));
        }
        return new HrOccasionsDto(today, q.Days, birthdays.OrderBy(o => o.Date).ThenBy(o => o.Number).ToList(), anniversaries.OrderBy(o => o.Date).ThenBy(o => o.Number).ToList());
    }

    /// <summary>The next occurrence of a yearly date on or after today (29 February falls on 28 February in other years).</summary>
    private static DateOnly Next(DateOnly original, DateOnly today)
    {
        DateOnly In(int year) => original.Month == 2 && original.Day == 29 && !DateTime.IsLeapYear(year) ? new DateOnly(year, 2, 28) : new DateOnly(year, original.Month, original.Day);
        var d = In(today.Year);
        if (d < today) d = In(today.Year + 1);
        return d;
    }

    private static List<(int Year, int Month)> Months(DateOnly from, DateOnly to)
    {
        var list = new List<(int, int)>();
        for (var m = new DateOnly(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1)) list.Add((m.Year, m.Month));
        return list;
    }
}
