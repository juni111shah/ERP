using ERP.Domain.Common;

namespace ERP.Modules.Hr.Domain;

public enum LoanKind { Loan = 1, Advance = 2 }
public enum LoanStatus { Active = 1, Settled = 2, Cancelled = 3 }

/// <summary>A staff loan or salary advance, recovered in instalments by the monthly payroll run.</summary>
[DefaultSort("-startYear")]
public class EmployeeLoan : LegalEntityScopedEntity
{
    [Filterable] public Guid EmployeeId { get; set; }
    [Filterable] public LoanKind Kind { get; set; } = LoanKind.Loan;
    public decimal Principal { get; set; }
    public decimal InstalmentAmount { get; set; }
    [Sortable, Filterable] public int StartYear { get; set; }
    [Sortable, Filterable] public int StartMonth { get; set; }
    public int Instalments { get; set; }
    public decimal Remaining { get; set; }
    [Filterable] public LoanStatus Status { get; set; } = LoanStatus.Active;
    [Searchable] public string? Reason { get; set; }
    public DateOnly? DisbursedOn { get; set; }
    public Guid? DisbursementJournalId { get; set; }
}

/// <summary>The part of one payroll line that repays a loan. Applied (the loan balance reduced) when the run is posted, restored when it is voided.</summary>
public class PayrollLoanRecovery : OrgEntity
{
    public Guid PayrollRunId { get; set; }
    public Guid PayrollLineId { get; set; }
    public Guid LoanId { get; set; }
    public decimal Amount { get; set; }
    public bool Applied { get; set; }
}
