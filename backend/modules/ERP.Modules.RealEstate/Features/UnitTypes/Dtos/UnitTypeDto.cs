using System.Linq.Expressions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record UnitTypeDto(Guid Id, string Code, string Name, UnitUsage DefaultUsage, int? DefaultBedrooms, bool IsActive)
{
    public static UnitTypeDto From(UnitType t) => new(t.Id, t.Code, t.Name, t.DefaultUsage, t.DefaultBedrooms, t.IsActive);

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<UnitType, UnitTypeDto>> Projection = t => new UnitTypeDto(t.Id, t.Code, t.Name, t.DefaultUsage, t.DefaultBedrooms, t.IsActive);
}
