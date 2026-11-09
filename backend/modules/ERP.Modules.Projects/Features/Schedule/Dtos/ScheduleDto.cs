using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

public sealed record ScheduleTaskDto(Guid Id, string Number, string Title, Guid? ParentTaskId, ProjectTaskStatus Status, DateOnly Start, DateOnly End, int DurationDays, int SlackDays, bool Critical, IReadOnlyList<Guid> DependsOn, string? AssignedTo,
    DateOnly? DueDate = null, bool Late = false, DateOnly? BaselineStart = null, DateOnly? BaselineEnd = null);
public sealed record ScheduleMilestoneDto(Guid Id, string Title, DateOnly Date, MilestoneStatus Status);
public sealed record ScheduleDto(Guid ProjectId, DateOnly Start, DateOnly Finish, IReadOnlyList<ScheduleTaskDto> Tasks, IReadOnlyList<Guid> CriticalPath, IReadOnlyList<ScheduleMilestoneDto>? Milestones = null, bool HasBaseline = false);
