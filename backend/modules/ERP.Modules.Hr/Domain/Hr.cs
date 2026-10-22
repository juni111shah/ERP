using ERP.Domain.Common;

namespace ERP.Modules.Hr.Domain;

public enum EmployeeStatus { Active = 1, OnLeave = 2, Terminated = 3 }
public enum PayrollStatus { Draft = 0, PendingApproval = 1, Posted = 2, Paid = 3 }

/// <summary>A member of staff of one employing legal entity, with the salary structure payroll uses. Country-specific identifiers (national id, WPS numbers) are custom fields from the country pack.</summary>
[DefaultSort("number")]
public class Employee : LegalEntityScopedEntity, IHasCustomFields
{
    [Listable] public string Number { get; set; } = default!;
    [Searchable, Sortable] public string FullName { get; set; } = default!;
    [Searchable, Filterable] public string Designation { get; set; } = default!;
    [Searchable, Filterable] public string? Department { get; set; }
    [Sortable, Filterable] public DateOnly JoinDate { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Nationality { get; set; }
    [Filterable, Sortable] public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    /// <summary>Monthly basic salary; also the end-of-service base.</summary>
    public decimal BasicSalary { get; set; }
    public decimal HousingAllowance { get; set; }
    public decimal TransportAllowance { get; set; }
    public decimal OtherAllowances { get; set; }
    /// <summary>Loan / advance recovery taken from every payslip.</summary>
    public decimal RecurringDeductions { get; set; }
    /// <summary>Sensitive: bank account used to pay the salary.</summary>
    public string? Iban { get; set; }
    public string? CustomFields { get; set; }

    public decimal GrossMonthly => BasicSalary + HousingAllowance + TransportAllowance + OtherAllowances;
    public decimal Allowances => HousingAllowance + TransportAllowance + OtherAllowances;

    /// <summary>On the payroll of a given month: joined by its last day and not left before its first.</summary>
    public bool IsPayableIn(DateOnly monthStart, DateOnly monthEnd)
        => JoinDate <= monthEnd && (TerminationDate is null || TerminationDate >= monthStart) && (Status != EmployeeStatus.Terminated || TerminationDate is not null);
}

/// <summary>Exceptions for one employee in one month. No record means full attendance and full pay.</summary>
public class AttendanceRecord : LegalEntityScopedEntity
{
    [Filterable] public Guid EmployeeId { get; set; }
    [Filterable] public int Year { get; set; }
    [Filterable] public int Month { get; set; }
    public int WorkingDays { get; set; } = 26;
    public int DaysPresent { get; set; }
    public int UnpaidAbsentDays { get; set; }
    public int PaidLeaveDays { get; set; }
    public decimal OvertimeHours { get; set; }
}

/// <summary>One month of payroll for one employing entity. Draft -> (PendingApproval) -> Posted (accrued) -> Paid.</summary>
[DefaultSort("-year")]
public class PayrollRun : LegalEntityScopedEntity
{
    [Listable] public string Number { get; set; } = default!;
    [Filterable, Sortable] public int Year { get; set; }
    [Filterable, Sortable] public int Month { get; set; }
    public DateOnly? PlannedPayDate { get; set; }
    [Filterable, Sortable] public PayrollStatus Status { get; set; }
    public string? Notes { get; set; }
    public int Employees { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal Deductions { get; set; }
    [Sortable] public decimal NetPay { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public Guid? AccrualJournalId { get; set; }
    public Guid? PaymentJournalId { get; set; }
    public DateOnly? PaidOn { get; set; }
    public List<PayrollLine> Lines { get; set; } = new();
}

public class PayrollLine : Entity
{
    public Guid PayrollRunId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = default!;
    public string? EmployeeNumber { get; set; }
    public int AbsentDays { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal Basic { get; set; }
    public decimal Allowances { get; set; }
    public decimal Overtime { get; set; }
    public decimal Absence { get; set; }
    /// <summary>One-off amount added (positive) or taken off (negative) by hand.</summary>
    public decimal Adjustment { get; set; }
    public string? AdjustmentNote { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal Deductions { get; set; }
    public decimal NetPay { get; set; }
}
