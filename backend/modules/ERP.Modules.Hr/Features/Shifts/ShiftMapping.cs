using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>Copies a shift input onto a shift (create and update).</summary>
internal static class ShiftMapping
{
    public static void Fill(Shift s, ShiftInput i, string code)
    {
        s.Code = code; s.Name = i.Name.Trim(); s.StartTime = i.StartTime; s.GraceMinutes = i.GraceMinutes; s.HoursPerDay = i.HoursPerDay; s.IsActive = i.IsActive;
    }
}
