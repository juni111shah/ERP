using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record EmployeeTaskInput(string Title, DateOnly? DueDate = null, ChecklistKind Kind = ChecklistKind.Onboarding, string? Notes = null);
public sealed record EmployeeTaskDto(Guid Id, Guid EmployeeId, ChecklistKind Kind, string Title, DateOnly? DueDate, bool IsDone, DateTime? DoneAt, string? Notes, bool Overdue)
{
    public static EmployeeTaskDto From(EmployeeTask x, DateOnly today) => new(x.Id, x.EmployeeId, x.Kind, x.Title, x.DueDate, x.IsDone, x.DoneAt, x.Notes, !x.IsDone && x.DueDate is { } d && d < today);
}
/// <summary>An open task of any employee, for the HR to-do list.</summary>
public sealed record OpenTaskDto(Guid Id, Guid EmployeeId, string EmployeeName, ChecklistKind Kind, string Title, DateOnly? DueDate, bool Overdue);
