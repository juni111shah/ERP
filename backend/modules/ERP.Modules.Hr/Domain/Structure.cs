using ERP.Domain.Common;

namespace ERP.Modules.Hr.Domain;

public enum ContractType { Permanent = 1, FixedTerm = 2, Probation = 3, PartTime = 4, Contractor = 5 }

/// <summary>People data of an employee: account link, line manager, structure and personal details. Kept apart from the pay structure in <c>Hr.cs</c>.</summary>
public partial class Employee
{
    /// <summary>The sign-in account of this person (at most one employee per account in a workspace); powers "my leave" and "my payslips".</summary>
    public Guid? UserId { get; set; }
    /// <summary>Line manager: gets leave requests first and may decide them.</summary>
    [Filterable] public Guid? ManagerEmployeeId { get; set; }
    [Filterable] public Guid? DepartmentId { get; set; }
    [Filterable] public Guid? PositionId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? MaritalStatus { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? Address { get; set; }
    [Filterable] public ContractType? ContractType { get; set; }
    public DateOnly? ProbationEndDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public int? NoticePeriodDays { get; set; }
    public string? BankName { get; set; }
}

/// <summary>Who prepared, adjusted and posted a payroll run; used by the optional separation-of-duties rule.</summary>
public partial class PayrollRun
{
    public Guid? GeneratedByUserId { get; set; }
    public Guid? LastAdjustedByUserId { get; set; }
    public Guid? PostedByUserId { get; set; }
}

/// <summary>A unit of the organisation (division, team ...). Departments nest through the parent.</summary>
[DefaultSort("code")]
public class Department : OrgEntity
{
    [Listable] public string Code { get; set; } = default!;
    [Searchable, Sortable] public string Name { get; set; } = default!;
    [Filterable] public Guid? ParentDepartmentId { get; set; }
    public Guid? HeadEmployeeId { get; set; }
    public Guid? BusinessUnitId { get; set; }
    [Filterable] public bool IsActive { get; set; } = true;
}

/// <summary>A job title with its grade.</summary>
[DefaultSort("code")]
public class Position : OrgEntity
{
    [Listable] public string Code { get; set; } = default!;
    [Searchable, Sortable] public string Title { get; set; } = default!;
    public string? Grade { get; set; }
    [Filterable] public bool IsActive { get; set; } = true;
}
