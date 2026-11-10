using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record ShiftInput(string Code, string Name, TimeOnly StartTime, int GraceMinutes = 10, int HoursPerDay = 8, bool IsActive = true);
public sealed record ShiftDto(Guid Id, string Code, string Name, TimeOnly StartTime, int GraceMinutes, int HoursPerDay, bool IsActive, int Employees)
{
    public static ShiftDto From(Shift s, int employees) => new(s.Id, s.Code, s.Name, s.StartTime, s.GraceMinutes, s.HoursPerDay, s.IsActive, employees);
}
public sealed record AssignShiftInput(IReadOnlyList<Guid> EmployeeIds);
public sealed record RosterInput(IReadOnlyList<Guid> EmployeeIds, DateOnly FromDate, DateOnly ToDate);
public sealed record RosterDto(Guid Id, Guid EmployeeId, Guid ShiftId, DateOnly FromDate, DateOnly ToDate)
{
    public static RosterDto From(ShiftRoster r) => new(r.Id, r.EmployeeId, r.ShiftId, r.FromDate, r.ToDate);
}
