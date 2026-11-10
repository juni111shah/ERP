using System.Text.Json.Nodes;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record UnitInput(
    Guid PropertyId, string UnitNumber, Guid UnitTypeId, UnitUsage? Usage, string? Floor, int? Bedrooms, decimal AreaSqft, decimal AskingRent,
    string? Notes = null, JsonObject? CustomFields = null);

public sealed record BulkUnitsInput(
    Guid PropertyId, Guid UnitTypeId, int FromFloor, int ToFloor, int UnitsPerFloor, decimal AreaSqft, decimal AskingRent, string? Prefix = null);

public enum UnitAction { Reserve, ReleaseReservation, MakeUnavailable, MakeAvailable }
