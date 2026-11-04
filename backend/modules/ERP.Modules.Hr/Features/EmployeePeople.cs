using System.Linq.Expressions;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using ERP.Modules.Hr.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Hr.Features;

/// <summary>Builds the employee detail, hiding pay unless the caller may see it.</summary>
public sealed class EmployeeDtoFactory(
    IReadRepository<LegalEntity> legalEntities, IReadRepository<Employee> employees, IReadRepository<LeaveRequest> leave, GratuityCalculator gratuity, ICustomFieldService fields,
    IUserDirectory users, ICurrentUser user, IClock clock)
{
    public async Task<EmployeeDto> BuildAsync(Employee e, bool includePay, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var service = gratuity.Calculate(e.BasicSalary, e.JoinDate, e.TerminationDate, today, await CountryAsync(e.LegalEntityId, ct));
        string? iban = null;
        if (includePay) iban = e.Iban is { Length: > 4 } v && !user.HasPermission(CorePermissions.SensitiveRead) ? new string('*', v.Length - 4) + v[^4..] : e.Iban;
        string? userName = null;
        if (e.UserId is { } uid) userName = (await users.ListMembersAsync([uid], ct)).FirstOrDefault()?.FullName;
        string? managerName = null;
        if (e.ManagerEmployeeId is { } mid) managerName = (await employees.FindAsync(mid, ct))?.FullName;
        var id = e.Id;
        var onLeave = await leave.AnyAsync(r => r.EmployeeId == id && r.Status == LeaveStatus.Approved && r.FromDate <= today && r.ToDate >= today, ct);
        return new EmployeeDto(e.Id, e.Number, e.LegalEntityId, e.FullName, e.Designation, e.Department, e.JoinDate, e.TerminationDate, e.Email, e.Phone, e.Nationality, e.Status,
            includePay ? e.BasicSalary : null, includePay ? e.HousingAllowance : null, includePay ? e.TransportAllowance : null, includePay ? e.OtherAllowances : null, includePay ? e.RecurringDeductions : null,
            iban, includePay ? e.GrossMonthly : null, service.ServiceYears, includePay ? service.Amount : null, await fields.ReadAsync(Docs.Employee, e, ct), e.LabourCardNumber, e.BankRoutingCode,
            e.UserId, userName, e.ManagerEmployeeId, managerName, e.DepartmentId, e.PositionId, e.DateOfBirth, e.Gender, e.MaritalStatus, e.EmergencyContactName, e.EmergencyContactPhone, e.Address,
            e.ContractType, e.ProbationEndDate, e.ContractEndDate, e.NoticePeriodDays, e.BankName, onLeave);
    }

    private async Task<string> CountryAsync(Guid legalEntityId, CancellationToken ct)
        => await legalEntities.Query(new Specification<LegalEntity>(l => l.Id == legalEntityId)).Select(l => l.CountryCode).FirstOrDefaultAsync(ct) ?? "";
}

/// <summary>Rules for the people side of an employee: account link, manager, department, position, history.</summary>
public sealed class EmployeePeople(
    IReadRepository<Employee> employees, IReadRepository<Department> departments, IReadRepository<Position> positions, IReadRepository<LeaveRequest> leave,
    IReadRepository<AttendanceRecord> attendance, IReadRepository<PayrollLine> payrollLines, IReadRepository<PerformanceReview> reviews, IUserDirectory users)
{
    /// <summary>Validates and copies the account link, manager, structure and personal details onto the employee.</summary>
    public async Task ApplyAsync(EmployeeInput i, Employee e, CancellationToken ct)
    {
        if (i.UserId is { } uid)
        {
            if ((await users.ListMembersAsync([uid], ct)).Count == 0) throw new FieldValidationException("userId", "That user is not a member of this workspace.");
            var self = e.Id;
            if (await employees.AnyAsync(x => x.UserId == uid && x.Id != self, ct)) throw new ConflictException("That user is already linked to another employee.");
        }
        e.UserId = i.UserId;

        if (i.ManagerEmployeeId is { } mid)
        {
            if (mid == e.Id) throw new FieldValidationException("managerEmployeeId", "An employee cannot be their own manager.");
            var manager = await employees.FindAsync(mid, ct) ?? throw new FieldValidationException("managerEmployeeId", "Manager not found.");
            var map = await employees.Query().Select(x => new { x.Id, x.ManagerEmployeeId }).ToDictionaryAsync(x => x.Id, x => x.ManagerEmployeeId, ct);
            if (Hierarchy.WouldCycle(e.Id, manager.Id, id => map.GetValueOrDefault(id))) throw new ConflictException("That manager already reports (directly or not) to this employee.");
        }
        e.ManagerEmployeeId = i.ManagerEmployeeId;

        e.DepartmentId = i.DepartmentId;
        if (i.DepartmentId is { } did)
        {
            var d = await departments.FindAsync(did, ct) ?? throw new FieldValidationException("departmentId", "Department not found.");
            e.Department = d.Name;
        }
        e.PositionId = i.PositionId;
        if (i.PositionId is { } pid)
        {
            var p = await positions.FindAsync(pid, ct) ?? throw new FieldValidationException("positionId", "Position not found.");
            e.Designation = p.Title;
        }

        e.DateOfBirth = i.DateOfBirth; e.Gender = Clean(i.Gender); e.MaritalStatus = Clean(i.MaritalStatus);
        e.EmergencyContactName = Clean(i.EmergencyContactName); e.EmergencyContactPhone = Clean(i.EmergencyContactPhone); e.Address = Clean(i.Address);
        e.ContractType = i.ContractType; e.ProbationEndDate = i.ProbationEndDate; e.ContractEndDate = i.ContractEndDate; e.NoticePeriodDays = i.NoticePeriodDays; e.BankName = Clean(i.BankName);
    }

    /// <summary>True when payroll, leave, attendance or review rows exist for the employee (they must be terminated, not deleted).</summary>
    public async Task<bool> HasHistoryAsync(Guid employeeId, CancellationToken ct)
        => await payrollLines.AnyAsync(x => x.EmployeeId == employeeId, ct)
           || await leave.AnyAsync(x => x.EmployeeId == employeeId, ct)
           || await attendance.AnyAsync(x => x.EmployeeId == employeeId, ct)
           || await reviews.AnyAsync(x => x.EmployeeId == employeeId, ct);

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

/// <summary>Employees list. Pay columns are projected only for callers with <c>hr.salary.read</c>; the rest see null. "On leave today" comes from approved leave covering today.</summary>
public sealed record ListEmployees(PagedQuery Paging) : IListRequest<PagedResult<EmployeeSummaryDto>>, IQuery<PagedResult<EmployeeSummaryDto>>, IRequirePermission
{
    public string RequiredPermission => P.EmployeesRead;

    private static readonly Expression<Func<Employee, EmployeeSummaryDto>> WithPay = e => new EmployeeSummaryDto(e.Id, e.Number, e.FullName, e.Designation, e.Department, e.LegalEntityId, e.JoinDate, e.Status,
        e.BasicSalary, e.HousingAllowance, e.TransportAllowance, e.OtherAllowances, e.DepartmentId, e.PositionId, e.ManagerEmployeeId, e.UserId, false);

    private static readonly Expression<Func<Employee, EmployeeSummaryDto>> WithoutPay = e => new EmployeeSummaryDto(e.Id, e.Number, e.FullName, e.Designation, e.Department, e.LegalEntityId, e.JoinDate, e.Status,
        null, null, null, null, e.DepartmentId, e.PositionId, e.ManagerEmployeeId, e.UserId, false);

    public async Task<PagedResult<EmployeeSummaryDto>> ExecuteAsync(IServiceProvider services, CancellationToken ct)
    {
        var user = services.GetRequiredService<ICurrentUser>();
        var page = await services.GetRequiredService<IReadRepository<Employee>>().PageAsync(Paging, user.HasPermission(P.SalaryRead) ? WithPay : WithoutPay, ct: ct);
        var ids = page.Items.Select(x => x.Id).ToList();
        if (ids.Count == 0) return page;
        var today = DateOnly.FromDateTime(services.GetRequiredService<IClock>().UtcNow);
        var away = (await services.GetRequiredService<IReadRepository<LeaveRequest>>()
            .Query(new Specification<LeaveRequest>(r => r.Status == LeaveStatus.Approved && r.FromDate <= today && r.ToDate >= today && ids.Contains(r.EmployeeId)))
            .Select(r => r.EmployeeId).ToListAsync(ct)).ToHashSet();
        return page.Map(x => x with { OnLeaveToday = away.Contains(x.Id) });
    }
}

/// <summary>Cycle check for parent / manager links.</summary>
public static class Hierarchy
{
    /// <summary>True when making <paramref name="newParent"/> the parent of <paramref name="id"/> would loop (it is the item itself or one of its descendants).</summary>
    public static bool WouldCycle(Guid id, Guid? newParent, Func<Guid, Guid?> parentOf)
    {
        var seen = new HashSet<Guid>();
        for (var cursor = newParent; cursor is { } c; cursor = parentOf(c))
        {
            if (c == id) return true;
            if (!seen.Add(c)) return false; // an existing loop elsewhere is not caused by this link
        }
        return false;
    }
}
