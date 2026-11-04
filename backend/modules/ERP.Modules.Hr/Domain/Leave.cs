using ERP.Domain.Common;

namespace ERP.Modules.Hr.Domain;

public enum LeaveStatus { Draft = 0, Submitted = 1, Approved = 2, Rejected = 3, Cancelled = 4 }

/// <summary>Upfront = the whole year's entitlement can be booked from 1 January; Monthly = one twelfth becomes available each month.</summary>
public enum LeaveAccrualMode { Upfront = 0, Monthly = 1 }

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
    /// <summary>In the year an employee joins, the entitlement is annual x remaining months / 12, rounded to the nearest half day.</summary>
    public bool ProRataFirstYear { get; set; }
    public LeaveAccrualMode AccrualMode { get; set; } = LeaveAccrualMode.Upfront;
    /// <summary>A request longer than this many days needs a supporting document before it can be submitted; null = never.</summary>
    public int? RequiresAttachmentAfterDays { get; set; }
    /// <summary>The unused balance of this type is paid out when the employee leaves.</summary>
    public bool Encashable { get; set; }
    /// <summary>Pay bands for sick-type leave (from day N of the year, X% pay). Empty = the IsPaid flag decides.</summary>
    public List<LeavePayBand> PayBands { get; set; } = [];
    /// <summary>Service-based entitlement tiers (empty = AnnualDays for everybody).</summary>
    public List<LeaveEntitlementTier> Tiers { get; set; } = [];
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
    /// <summary>The first day counts 0.5 (a one-day request with either flag is 0.5).</summary>
    public bool StartHalfDay { get; set; }
    /// <summary>The last day counts 0.5.</summary>
    public bool EndHalfDay { get; set; }
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

/// <summary>From day <see cref="FromDay"/> (counted per employee per year of the leave type) the leave pays <see cref="PayPercent"/> of the daily rate.</summary>
public class LeavePayBand : OrgEntity
{
    public Guid LeaveTypeId { get; set; }
    public int FromDay { get; set; }
    public decimal PayPercent { get; set; }
}
