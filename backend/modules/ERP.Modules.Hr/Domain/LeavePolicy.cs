using ERP.Domain.Common;

namespace ERP.Modules.Hr.Domain;

/// <summary>From this many completed years of service, the leave type grants Days a year (the tier with the highest FromServiceYears that the employee has reached applies).</summary>
public class LeaveEntitlementTier : OrgEntity
{
    public Guid LeaveTypeId { get; set; }
    public decimal FromServiceYears { get; set; }
    public decimal Days { get; set; }
}

/// <summary>How an approved request's days were deducted per calendar year (only written for approvals made after year-end splitting existed; a request without rows was deducted wholly from its start year).</summary>
public class LeaveRequestYearDays : OrgEntity
{
    public Guid RequestId { get; set; }
    public int Year { get; set; }
    public decimal Days { get; set; }
}
