using ERP.Domain.Common;

namespace ERP.Modules.Hr.Domain;

public enum LeaveStatus { Draft = 0, Submitted = 1, Approved = 2, Rejected = 3, Cancelled = 4 }

/// <summary>A kind of leave (annual, sick, unpaid ...). Annual days and the carry-forward cap seed each employee's yearly balance.</summary>
[DefaultSort("code")]
public class LeaveType : OrgEntity
{
    [Listable] public string Code { get; set; } = default!;
    [Listable] public string Name { get; set; } = default!;
    /// <summary>Paid leave does not reduce pay; unpaid leave is deducted like an unpaid absence in payroll.</summary>
    [Filterable] public bool IsPaid { get; set; } = true;
    /// <summary>Days granted each year; 0 means no balance is enforced (for example unpaid leave).</summary>
    public decimal AnnualDays { get; set; }
    /// <summary>Most days that roll into next year; 0 = nothing carries over.</summary>
    public decimal CarryForwardCap { get; set; }
    public bool RequiresApproval { get; set; } = true;
    [Filterable] public bool IsActive { get; set; } = true;
}

/// <summary>One employee's balance of one leave type for one year. Taken is kept in step with approved requests; remaining = entitled + carried - taken.</summary>
public class LeaveBalance : OrgEntity
{
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public int Year { get; set; }
    public decimal Entitled { get; set; }
    public decimal Carried { get; set; }
    public decimal Taken { get; set; }
}

/// <summary>A request for leave. Days exclude weekends (setting hr.weekendDays) and public holidays. Draft -> Submitted -> Approved | Rejected; Cancelled at any time before it is over.</summary>
[DefaultSort("-fromDate")]
public class LeaveRequest : LegalEntityScopedEntity
{
    [Filterable] public Guid EmployeeId { get; set; }
    [Searchable] public string EmployeeName { get; set; } = default!;
    [Filterable] public Guid LeaveTypeId { get; set; }
    [Sortable, Filterable] public DateOnly FromDate { get; set; }
    [Sortable, Filterable] public DateOnly ToDate { get; set; }
    public decimal Days { get; set; }
    [Filterable] public LeaveStatus Status { get; set; } = LeaveStatus.Draft;
    [Searchable] public string? Reason { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionComment { get; set; }
}

/// <summary>A non-working day for everyone (national or company holiday); excluded from leave day counts.</summary>
[DefaultSort("date")]
public class PublicHoliday : OrgEntity
{
    [Sortable, Filterable] public DateOnly Date { get; set; }
    [Searchable] public string Name { get; set; } = default!;
}
