using ERP.Domain.Common;

namespace ERP.Modules.Hr.Domain;

public enum HistoryKind { Joined = 1, Salary = 2, Designation = 3, Department = 4, Position = 5, Manager = 6, Contract = 7, Status = 8, Terminated = 9, Note = 10 }

/// <summary>One change in an employee's working life (join, pay rise, promotion, transfer, contract change, leaving ...), written automatically when the record changes.</summary>
[DefaultSort("-effectiveDate")]
public class EmployeeHistoryEntry : LegalEntityScopedEntity
{
    [Filterable] public Guid EmployeeId { get; set; }
    [Sortable, Filterable] public DateOnly EffectiveDate { get; set; }
    [Filterable] public HistoryKind Kind { get; set; }
    public string Summary { get; set; } = default!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    /// <summary>Pay information: shown only to people who may see salaries.</summary>
    public bool IsPay { get; set; }
    public Guid? ChangedByUserId { get; set; }
}
