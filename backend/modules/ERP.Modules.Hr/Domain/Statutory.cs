using ERP.Domain.Common;

namespace ERP.Modules.Hr.Domain;

public enum StatutoryKind { EmployeeDeduction = 1, EmployerCost = 2 }
public enum StatutoryMethod { Flat = 1, Slab = 2 }
public enum StatutoryBase { Basic = 1, Gross = 2 }

/// <summary>
/// A statutory payroll rule of one employing entity: a percentage of basic or gross pay (flat), or progressive bands (slab), taken from the employee
/// (social security, income tax, pension ...) or carried by the employer. Rules are data, so any country's scheme is set up here, not in code.
/// </summary>
[DefaultSort("code")]
public class StatutoryRule : LegalEntityScopedEntity
{
    [Listable] public string Code { get; set; } = default!;
    [Searchable, Sortable] public string Name { get; set; } = default!;
    [Filterable] public StatutoryKind Kind { get; set; } = StatutoryKind.EmployeeDeduction;
    public StatutoryMethod Method { get; set; } = StatutoryMethod.Flat;
    public StatutoryBase Base { get; set; } = StatutoryBase.Basic;
    /// <summary>Flat: percent of the base.</summary>
    public decimal Rate { get; set; }
    /// <summary>Nothing is taken when the base is below this monthly amount (0 = no threshold).</summary>
    public decimal Threshold { get; set; }
    /// <summary>The base counts up to this monthly amount only (0 = no cap).</summary>
    public decimal Cap { get; set; }
    /// <summary>Comma separated nationalities the rule applies to (blank = everyone). Prefix with "!" to exclude them, for example "!Qatari".</summary>
    public string? Nationalities { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    [Filterable] public bool IsActive { get; set; } = true;
    public List<StatutorySlab> Slabs { get; set; } = [];
}

/// <summary>Slab method: <see cref="Rate"/> percent applies to the part of the base above <see cref="FromAmount"/> and below the next slab.</summary>
public class StatutorySlab : OrgEntity
{
    public Guid RuleId { get; set; }
    public decimal FromAmount { get; set; }
    public decimal Rate { get; set; }
}

/// <summary>What one rule took from (or cost the employer for) one payroll line: the filing and payslip detail.</summary>
public class PayrollLineStatutory : OrgEntity
{
    public Guid PayrollRunId { get; set; }
    public Guid PayrollLineId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid RuleId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public StatutoryKind Kind { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal Amount { get; set; }
}
