using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for employees.</summary>
public interface IEmployeeRepository : IRepository<Employee>
{
    /// <summary>Every employee not terminated, as an org-chart source row.</summary>
    Task<List<OrgPersonSource>> ListOrgPeopleAsync(CancellationToken ct);

    /// <summary>The IBAN of each of the employees, by employee id.</summary>
    Task<Dictionary<Guid, string?>> IbansByIdAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The employees with the given ids, by id.</summary>
    Task<Dictionary<Guid, Employee>> MapByIdsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>Every employee of a legal entity, whatever the status.</summary>
    Task<List<Employee>> ListOfEntityAsync(Guid legalEntityId, CancellationToken ct);

    /// <summary>The department of each of the employees.</summary>
    Task<List<EmployeeDepartment>> ListDepartmentsAsync(IReadOnlyCollection<Guid> employeeIds, CancellationToken ct);

    /// <summary>The full name of an employee; empty when not found.</summary>
    Task<string> GetNameAsync(Guid id, CancellationToken ct);

    /// <summary>The employees (of one entity, or all) who joined on or before <paramref name="asOf"/> and are not terminated, by number.</summary>
    Task<List<Employee>> ListNotTerminatedJoinedByAsync(Guid? legalEntityId, DateOnly asOf, CancellationToken ct);

    /// <summary>Designation, department and email of an employee, for a payslip; null when not found.</summary>
    Task<EmployeePayslipDetails?> FindPayslipDetailsAsync(Guid id, CancellationToken ct);

    /// <summary>The employees (of one entity and/or one employee, or all) on the payroll at some point from <paramref name="from"/> to <paramref name="to"/>, by number.</summary>
    Task<List<Employee>> ListPayableInAsync(Guid? legalEntityId, Guid? employeeId, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>The employee linked to a user account; null when there is none.</summary>
    Task<Employee?> FindByUserAsync(Guid userId, CancellationToken ct);

    /// <summary>The ids of the employees whose line manager is <paramref name="managerEmployeeId"/>.</summary>
    Task<List<Guid>> ListReportIdsAsync(Guid managerEmployeeId, CancellationToken ct);

    /// <summary>The employee with the number; null when there is none.</summary>
    Task<Employee?> FindByNumberAsync(string number, CancellationToken ct);

    /// <summary>The employees with the given ids, read-only.</summary>
    Task<List<Employee>> ListByIdsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The employees with the given ids, tracked for changes.</summary>
    Task<List<Employee>> ListByIdsForUpdateAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>How many employees are on the shift.</summary>
    Task<int> CountOnShiftAsync(Guid shiftId, CancellationToken ct);

    /// <summary>How many employees are on each shift, by shift id.</summary>
    Task<Dictionary<Guid, int>> CountByShiftAsync(CancellationToken ct);

    /// <summary>Number and name of those of the employees who are not terminated, by id.</summary>
    Task<Dictionary<Guid, EmployeeLabel>> MapLabelsOfNotTerminatedAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The full name of each of the employees, by id.</summary>
    Task<Dictionary<Guid, string>> NamesByIdAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>What the HR summary report reads of every employee (of one entity, or all).</summary>
    Task<List<EmployeeReportRow>> ListReportRowsAsync(Guid? legalEntityId, CancellationToken ct);

    /// <summary>Join date and birth date of the employees not terminated (of one entity, or all).</summary>
    Task<List<EmployeeOccasionRow>> ListOccasionRowsAsync(Guid? legalEntityId, CancellationToken ct);
}

/// <summary>What the HR summary report reads of an employee.</summary>
public sealed record EmployeeReportRow(Guid Id, string? Department, string? Nationality, DateOnly JoinDate, DateOnly? TerminationDate, EmployeeStatus Status, ContractType? ContractType, string? CustomFields);

/// <summary>What the birthdays and anniversaries report reads of an employee.</summary>
public sealed record EmployeeOccasionRow(Guid Id, string Number, string FullName, DateOnly JoinDate, DateOnly? DateOfBirth, string? CustomFields);

/// <summary>An employee's number and name.</summary>
public sealed record EmployeeLabel(Guid Id, string Number, string FullName);

/// <summary>What a payslip shows about the employee.</summary>
public sealed record EmployeePayslipDetails(string Designation, string? Department, string? Email);

/// <summary>An employee and the department they belong to (null: none).</summary>
public sealed record EmployeeDepartment(Guid Id, Guid? DepartmentId);
