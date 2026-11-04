using ERP.Domain.Common;

namespace ERP.Modules.Hr.Domain;

public enum ChecklistKind { Onboarding = 1, Offboarding = 2 }
public enum CandidateStage { Applied = 1, Screening = 2, Interview = 3, Offer = 4, Hired = 5, Rejected = 6 }
public enum OpeningStatus { Open = 1, OnHold = 2, Closed = 3 }

/// <summary>A reusable list of steps for a new joiner or a leaver (laptop, access, contract signed, exit interview ...).</summary>
[DefaultSort("name")]
public class ChecklistTemplate : OrgEntity
{
    [Searchable, Sortable] public string Name { get; set; } = default!;
    [Filterable] public ChecklistKind Kind { get; set; } = ChecklistKind.Onboarding;
    [Filterable] public bool IsActive { get; set; } = true;
    public List<ChecklistTemplateItem> Items { get; set; } = [];
}

public class ChecklistTemplateItem : OrgEntity
{
    public Guid TemplateId { get; set; }
    public int Position { get; set; }
    public string Title { get; set; } = default!;
    /// <summary>Due this many days after the join date (onboarding) or before the last day (offboarding, use a negative number).</summary>
    public int DueOffsetDays { get; set; }
}

/// <summary>One step of an employee's onboarding or offboarding.</summary>
[DefaultSort("dueDate")]
public class EmployeeTask : LegalEntityScopedEntity
{
    [Filterable] public Guid EmployeeId { get; set; }
    [Filterable] public ChecklistKind Kind { get; set; }
    [Searchable] public string Title { get; set; } = default!;
    [Sortable, Filterable] public DateOnly? DueDate { get; set; }
    [Filterable] public bool IsDone { get; set; }
    public DateTime? DoneAt { get; set; }
    public Guid? DoneByUserId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>A vacancy.</summary>
[DefaultSort("-createdAt")]
public class JobOpening : OrgEntity
{
    [Searchable, Sortable] public string Title { get; set; } = default!;
    public Guid? DepartmentId { get; set; }
    public Guid? PositionId { get; set; }
    public int Headcount { get; set; } = 1;
    [Filterable] public OpeningStatus Status { get; set; } = OpeningStatus.Open;
    public string? Description { get; set; }
    public DateOnly? ClosingDate { get; set; }
}

[DefaultSort("-createdAt")]
public class Candidate : OrgEntity
{
    [Filterable] public Guid OpeningId { get; set; }
    [Searchable, Sortable] public string FullName { get; set; } = default!;
    [Searchable] public string? Email { get; set; }
    public string? Phone { get; set; }
    [Filterable, Sortable] public CandidateStage Stage { get; set; } = CandidateStage.Applied;
    public string? Source { get; set; }
    public decimal? ExpectedSalary { get; set; }
    public string? Notes { get; set; }
    public DateTime StageChangedAt { get; set; }
    /// <summary>The employee record created when the candidate was hired.</summary>
    public Guid? EmployeeId { get; set; }
}
