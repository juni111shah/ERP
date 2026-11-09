using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>Minutes worked, late and overtime for one day; no I/O.</summary>
public static class DailyAttendanceMath
{
    public sealed record Result(int Worked, int Late, int Overtime);

    public static Result Compute(TimeOnly? checkIn, TimeOnly? checkOut, TimeOnly shiftStart, int graceMinutes, int hoursPerDay)
    {
        if (checkIn is not { } inT) return new Result(0, 0, 0);
        var late = Math.Max(0, (int)(inT - shiftStart).TotalMinutes - graceMinutes);
        if (late > 0) late += graceMinutes; // beyond the grace period the whole delay counts, not only the part after it
        if (checkOut is not { } outT || outT <= inT) return new Result(0, late, 0);
        var worked = (int)(outT - inT).TotalMinutes;
        return new Result(worked, late, Math.Max(0, worked - hoursPerDay * 60));
    }

    public static DailyAttendanceStatus StatusOf(DailyAttendanceStatus? given, TimeOnly? checkIn) => given ?? (checkIn is null ? DailyAttendanceStatus.Absent : DailyAttendanceStatus.Present);
}
