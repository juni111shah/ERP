using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record UnitTypeInput(string Code, string Name, UnitUsage DefaultUsage, int? DefaultBedrooms, bool IsActive = true);
