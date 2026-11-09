using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>Copies a position input onto a position (create and update).</summary>
internal static class PositionMapping
{
    public static void Fill(Position p, PositionInput i, string code)
    {
        p.Code = code; p.Title = i.Title.Trim(); p.Grade = string.IsNullOrWhiteSpace(i.Grade) ? null : i.Grade.Trim(); p.IsActive = i.IsActive;
    }
}
