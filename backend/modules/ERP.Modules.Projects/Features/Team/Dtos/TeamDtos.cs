namespace ERP.Modules.Projects.Features;

public sealed record MemberDto(Guid Id, Guid ProjectId, Guid UserId, string Name, string? Role);
public sealed record AllocationInput(Guid ProjectId, Guid UserId, DateOnly FromDate, DateOnly ToDate, decimal HoursPerWeek);
public sealed record AllocationDto(Guid Id, Guid ProjectId, Guid UserId, string UserName, DateOnly FromDate, DateOnly ToDate, decimal HoursPerWeek);
