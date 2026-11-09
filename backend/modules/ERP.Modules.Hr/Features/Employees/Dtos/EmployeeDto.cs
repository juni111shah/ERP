using System.Linq.Expressions;
using System.Text.Json.Nodes;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record EmployeeInput(
    Guid LegalEntityId, string? Number, string FullName, string? Designation, string? Department, DateOnly JoinDate, DateOnly? TerminationDate, string? Email, string? Phone, string? Nationality,
    EmployeeStatus Status, decimal? BasicSalary, decimal? HousingAllowance, decimal? TransportAllowance, decimal? OtherAllowances, decimal? RecurringDeductions, string? Iban, JsonObject? CustomFields = null, string? LabourCardNumber = null, string? BankRoutingCode = null,
    Guid? UserId = null, Guid? ManagerEmployeeId = null, Guid? DepartmentId = null, Guid? PositionId = null, DateOnly? DateOfBirth = null, string? Gender = null, string? MaritalStatus = null,
    string? EmergencyContactName = null, string? EmergencyContactPhone = null, string? Address = null, ContractType? ContractType = null, DateOnly? ProbationEndDate = null, DateOnly? ContractEndDate = null,
    int? NoticePeriodDays = null, string? BankName = null);

/// <summary>Pay fields (salary, allowances, deductions, IBAN, gross, gratuity) are null for callers without <c>hr.salary.read</c>.</summary>
public sealed record EmployeeDto(
    Guid Id, string Number, Guid LegalEntityId, string FullName, string Designation, string? Department, DateOnly JoinDate, DateOnly? TerminationDate, string? Email, string? Phone, string? Nationality,
    EmployeeStatus Status, decimal? BasicSalary, decimal? HousingAllowance, decimal? TransportAllowance, decimal? OtherAllowances, decimal? RecurringDeductions, string? Iban, decimal? GrossMonthly,
    decimal ServiceYears, decimal? GratuityAccrued, JsonObject CustomFields, string? LabourCardNumber = null, string? BankRoutingCode = null,
    Guid? UserId = null, string? UserName = null, Guid? ManagerEmployeeId = null, string? ManagerName = null, Guid? DepartmentId = null, Guid? PositionId = null, DateOnly? DateOfBirth = null,
    string? Gender = null, string? MaritalStatus = null, string? EmergencyContactName = null, string? EmergencyContactPhone = null, string? Address = null, ContractType? ContractType = null,
    DateOnly? ProbationEndDate = null, DateOnly? ContractEndDate = null, int? NoticePeriodDays = null, string? BankName = null, bool OnLeaveToday = false);

/// <summary>List row. Gross is added up here (not in SQL) so it works on every database; pay is null without <c>hr.salary.read</c>.</summary>
public sealed record EmployeeSummaryDto(
    Guid Id, string Number, string FullName, string Designation, string? Department, Guid LegalEntityId, DateOnly JoinDate, EmployeeStatus Status,
    decimal? BasicSalary, decimal? HousingAllowance, decimal? TransportAllowance, decimal? OtherAllowances,
    Guid? DepartmentId = null, Guid? PositionId = null, Guid? ManagerEmployeeId = null, Guid? UserId = null, bool OnLeaveToday = false)
{
    public decimal? GrossMonthly => BasicSalary is null ? null : BasicSalary + (HousingAllowance ?? 0) + (TransportAllowance ?? 0) + (OtherAllowances ?? 0);

    /// <summary>The list projection for callers with <c>hr.salary.read</c>.</summary>
    public static readonly Expression<Func<Employee, EmployeeSummaryDto>> WithPay = e => new EmployeeSummaryDto(e.Id, e.Number, e.FullName, e.Designation, e.Department, e.LegalEntityId, e.JoinDate, e.Status,
        e.BasicSalary, e.HousingAllowance, e.TransportAllowance, e.OtherAllowances, e.DepartmentId, e.PositionId, e.ManagerEmployeeId, e.UserId, false);

    /// <summary>The list projection for everyone else: pay columns are null.</summary>
    public static readonly Expression<Func<Employee, EmployeeSummaryDto>> WithoutPay = e => new EmployeeSummaryDto(e.Id, e.Number, e.FullName, e.Designation, e.Department, e.LegalEntityId, e.JoinDate, e.Status,
        null, null, null, null, e.DepartmentId, e.PositionId, e.ManagerEmployeeId, e.UserId, false);
}
