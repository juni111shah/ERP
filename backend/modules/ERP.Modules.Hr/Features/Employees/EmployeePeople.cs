using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

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
