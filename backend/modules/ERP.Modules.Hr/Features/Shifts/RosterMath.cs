using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>Which roster entry (if any) covers a date; the latest start wins when entries overlap. No I/O.</summary>
public static class RosterMath
{
    public static Guid? ShiftOn(IEnumerable<ShiftRoster> entries, DateOnly date) =>
        entries.Where(r => r.FromDate <= date && date <= r.ToDate).OrderByDescending(r => r.FromDate).Select(r => (Guid?)r.ShiftId).FirstOrDefault();
}
