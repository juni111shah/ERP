using ERP.Application.Common;

namespace ERP.Modules.Projects.Features;

/// <summary>How this workspace runs its projects: approvals, daily limit, capacity, working days and the default rate.</summary>
public sealed record GetProjectsConfigQuery : IQuery<ProjectsConfigDto>;

public sealed record ProjectsConfigDto(bool TimesheetApproval, bool ExpenseApproval, bool AllowSelfApproval, decimal MaxHoursPerDay, decimal WeeklyCapacityHours, IReadOnlyList<int> WorkDays, decimal DefaultHourlyRate);
