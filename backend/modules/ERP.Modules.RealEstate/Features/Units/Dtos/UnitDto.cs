using System.Text.Json.Nodes;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

public sealed record UnitDto(
    Guid Id, Guid PropertyId, Guid LegalEntityId, string UnitNumber, Guid UnitTypeId, UnitUsage Usage, string? Floor, int? Bedrooms,
    decimal AreaSqft, decimal AskingRent, UnitStatus Status, DateOnly? VacantSince, int? DaysVacant,
    DateOnly? ReservedUntil, string? UnavailableReason, string? Notes, JsonObject CustomFields)
{
    public static UnitDto From(Unit u, DateOnly today, JsonObject customFields) => new(
        u.Id, u.PropertyId, u.LegalEntityId, u.UnitNumber, u.UnitTypeId, u.Usage, u.Floor, u.Bedrooms, u.AreaSqft, u.AskingRent, u.Status,
        u.VacantSince, u.Status == UnitStatus.Vacant && u.VacantSince is { } v ? Math.Max(0, today.DayNumber - v.DayNumber) : null,
        u.ReservedUntil, u.UnavailableReason, u.Notes, customFields);
}

/// <summary>A row of the units list (spec RE-UNIT-03): the unit with its type, property and current tenancy.</summary>
public sealed record UnitListItem(
    Guid Id, Guid PropertyId, string PropertyName, string UnitNumber, string UnitTypeName, UnitUsage Usage, string? Floor, decimal AreaSqft,
    decimal AskingRent, UnitStatus Status, string? TenantName, decimal? CurrentRent, DateOnly? LeaseEnds, int? DaysVacant);

public sealed record UnitRentRateDto(Guid Id, DateOnly EffectiveFrom, decimal AnnualRent, string? Reason);
