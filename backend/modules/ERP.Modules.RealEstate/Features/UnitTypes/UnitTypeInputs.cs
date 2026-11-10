using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Copies a unit type form onto the entity (shared by create and update).</summary>
internal static class UnitTypeInputs
{
    public static void Map(UnitTypeInput i, UnitType t, string code)
    {
        t.Code = code; t.Name = i.Name.Trim(); t.DefaultUsage = i.DefaultUsage; t.DefaultBedrooms = i.DefaultBedrooms; t.IsActive = i.IsActive;
    }
}
