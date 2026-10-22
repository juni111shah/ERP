using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

public sealed record UnitInput(
    Guid PropertyId, string UnitNumber, Guid UnitTypeId, UnitUsage? Usage, string? Floor, int? Bedrooms, decimal AreaSqft, decimal AskingRent,
    string? Notes = null, JsonObject? CustomFields = null);

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

public enum UnitAction { Reserve, ReleaseReservation, MakeUnavailable, MakeAvailable }

public sealed record BulkUnitsInput(
    Guid PropertyId, Guid UnitTypeId, int FromFloor, int ToFloor, int UnitsPerFloor, decimal AreaSqft, decimal AskingRent, string? Prefix = null);

/// <summary>Units of the caller's workspace. Standard search / filter / sort / paging, plus tenant and rent from the active lease.</summary>
public sealed record ListUnits : PagedQuery, IQuery<PagedResult<UnitListItem>>, IRequirePermission
{
    public string RequiredPermission => P.UnitsRead;
}

public sealed record GetUnit(Guid Id) : IQuery<UnitDto>, IRequirePermission
{
    public string RequiredPermission => P.UnitsRead;
}

public sealed record GetUnitRentHistory(Guid UnitId) : IQuery<IReadOnlyList<UnitRentRateDto>>, IRequirePermission
{
    public string RequiredPermission => P.UnitsRead;
}

public sealed record CreateUnit(UnitInput Input) : ICommand<UnitDto>, IRequirePermission
{
    public string RequiredPermission => P.UnitsManage;
}

public sealed record UpdateUnit(Guid Id, UnitInput Input) : ICommand<UnitDto>, IRequirePermission
{
    public string RequiredPermission => P.UnitsManage;
}

/// <summary>Reserve / release / make unavailable / make available (Occupied and Vacant-by-lease are set only by leases).</summary>
public sealed record ChangeUnitStatus(Guid Id, UnitAction Action, DateOnly? Until = null, string? Reason = null) : ICommand<UnitDto>, IRequirePermission
{
    public string RequiredPermission => Action is UnitAction.Reserve or UnitAction.ReleaseReservation ? P.UnitsReserve : P.UnitsManage;
}

public sealed record BulkCreateUnits(BulkUnitsInput Input) : ICommand<IReadOnlyList<UnitDto>>, IRequirePermission
{
    public string RequiredPermission => P.UnitsManage;
}

public sealed class UnitInputValidator : AbstractValidator<UnitInput>
{
    public UnitInputValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.UnitTypeId).NotEmpty();
        RuleFor(x => x.UnitNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.AreaSqft).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AskingRent).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Usage).IsInEnum().When(x => x.Usage is not null);
    }
}

public sealed class CreateUnitValidator : AbstractValidator<CreateUnit>
{
    public CreateUnitValidator() => RuleFor(x => x.Input).SetValidator(new UnitInputValidator());
}

public sealed class UpdateUnitValidator : AbstractValidator<UpdateUnit>
{
    public UpdateUnitValidator() => RuleFor(x => x.Input).SetValidator(new UnitInputValidator());
}

public sealed class BulkCreateUnitsValidator : AbstractValidator<BulkCreateUnits>
{
    public const int MaxUnits = 500;

    public BulkCreateUnitsValidator()
    {
        RuleFor(x => x.Input.PropertyId).NotEmpty();
        RuleFor(x => x.Input.UnitTypeId).NotEmpty();
        RuleFor(x => x.Input.FromFloor).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.ToFloor).GreaterThanOrEqualTo(x => x.Input.FromFloor);
        RuleFor(x => x.Input.UnitsPerFloor).InclusiveBetween(1, 99);
        RuleFor(x => x.Input.AreaSqft).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.AskingRent).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => (x.Input.ToFloor - x.Input.FromFloor + 1) * x.Input.UnitsPerFloor <= MaxUnits)
            .WithName("input").WithMessage($"Create at most {MaxUnits} units at a time.");
    }
}

internal sealed class UnitHandlers(
    IRepository<Unit> units, IRepository<Property> properties, IReadRepository<UnitType> unitTypes, IRepository<UnitRentRate> rates,
    IReadRepository<Lease> leases, IReadRepository<Party> parties, IReadRepository<WorkOrder> workOrders, ICustomFieldService fields, IClock clock) :
    IRequestHandler<ListUnits, PagedResult<UnitListItem>>,
    IRequestHandler<GetUnit, UnitDto>,
    IRequestHandler<GetUnitRentHistory, IReadOnlyList<UnitRentRateDto>>,
    IRequestHandler<CreateUnit, UnitDto>,
    IRequestHandler<UpdateUnit, UnitDto>,
    IRequestHandler<ChangeUnitStatus, UnitDto>,
    IRequestHandler<BulkCreateUnits, IReadOnlyList<UnitDto>>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<PagedResult<UnitListItem>> Handle(ListUnits q, CancellationToken ct)
    {
        var page = await units.PageAsync(q, ct: ct);
        var unitIds = page.Items.Select(u => u.Id).ToList();
        var propertyIds = page.Items.Select(u => u.PropertyId).Distinct().ToList();
        var typeIds = page.Items.Select(u => u.UnitTypeId).Distinct().ToList();
        var propertyNames = await properties.Query(new Specification<Property>(p => propertyIds.Contains(p.Id))).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var typeNames = await unitTypes.Query(new Specification<UnitType>(t => typeIds.Contains(t.Id))).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var active = await leases.Query(new Specification<Lease>(l => unitIds.Contains(l.UnitId) && (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.HoldingOver))).ToListAsync(ct);
        var tenantIds = active.Select(l => l.LesseePartyId).Distinct().ToList();
        var tenantNames = await parties.Query(new Specification<Party>(p => tenantIds.Contains(p.Id))).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var leaseByUnit = active.GroupBy(l => l.UnitId).ToDictionary(g => g.Key, g => g.First());

        return page.Map(u =>
        {
            leaseByUnit.TryGetValue(u.Id, out var lease);
            return new UnitListItem(
                u.Id, u.PropertyId, propertyNames.GetValueOrDefault(u.PropertyId, ""), u.UnitNumber, typeNames.GetValueOrDefault(u.UnitTypeId, ""), u.Usage, u.Floor,
                u.AreaSqft, u.AskingRent, u.Status, lease is null ? null : tenantNames.GetValueOrDefault(lease.LesseePartyId), lease?.AnnualRent, lease?.EndDate,
                u.Status == UnitStatus.Vacant && u.VacantSince is { } since ? Math.Max(0, Today.DayNumber - since.DayNumber) : null);
        });
    }

    public async Task<UnitDto> Handle(GetUnit q, CancellationToken ct)
    {
        var unit = await units.GetAsync(q.Id, ct);
        return UnitDto.From(unit, Today, await fields.ReadAsync(Docs.Unit, unit, ct));
    }

    public async Task<IReadOnlyList<UnitRentRateDto>> Handle(GetUnitRentHistory q, CancellationToken ct)
    {
        await units.GetAsync(q.UnitId, ct); // 404 for units outside the caller's scope
        return await rates.Query(new Specification<UnitRentRate>(r => r.UnitId == q.UnitId)).OrderByDescending(r => r.EffectiveFrom)
            .Select(r => new UnitRentRateDto(r.Id, r.EffectiveFrom, r.AnnualRent, r.Reason)).ToListAsync(ct);
    }

    public async Task<UnitDto> Handle(CreateUnit cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var property = await properties.FindAsync(i.PropertyId, ct) ?? throw new FieldValidationException("propertyId", "Property not found.");
        var type = await unitTypes.FindAsync(i.UnitTypeId, ct) ?? throw new FieldValidationException("unitTypeId", "Unit type not found.");
        var number = i.UnitNumber.Trim();
        if (await units.AnyAsync(u => u.PropertyId == property.Id && u.UnitNumber == number, ct))
            throw new ConflictException($"Unit {number} already exists in {property.Name}.");

        var unit = Unit.Create(Today);
        unit.PropertyId = property.Id;
        unit.LegalEntityId = property.LegalEntityId; // copied for entity scoping
        Map(i, unit, type, number);
        await fields.ApplyAsync(Docs.Unit, unit, i.CustomFields, isCreate: true, ct);
        units.Add(unit);
        if (unit.AskingRent > 0) AddRate(unit, "Initial asking rent");
        await GrowTotalUnitsAsync(property.Id, 1, ct);
        await units.SaveChangesAsync(ct);
        return UnitDto.From(unit, Today, await fields.ReadAsync(Docs.Unit, unit, ct));
    }

    public async Task<UnitDto> Handle(UpdateUnit cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var unit = await units.GetForUpdateAsync(cmd.Id, ct);
        if (i.PropertyId != unit.PropertyId) throw new FieldValidationException("propertyId", "A unit cannot move to another property.");
        var type = await unitTypes.FindAsync(i.UnitTypeId, ct) ?? throw new FieldValidationException("unitTypeId", "Unit type not found.");
        var number = i.UnitNumber.Trim();
        if (number != unit.UnitNumber && await units.AnyAsync(u => u.PropertyId == unit.PropertyId && u.UnitNumber == number, ct))
            throw new ConflictException($"Unit {number} already exists in this property.");

        var oldRent = unit.AskingRent;
        Map(i, unit, type, number);
        await fields.ApplyAsync(Docs.Unit, unit, i.CustomFields, isCreate: false, ct);
        if (unit.AskingRent != oldRent) await RecordRateAsync(unit, ct);
        await units.SaveChangesAsync(ct);
        return UnitDto.From(unit, Today, await fields.ReadAsync(Docs.Unit, unit, ct));
    }

    public async Task<UnitDto> Handle(ChangeUnitStatus cmd, CancellationToken ct)
    {
        var unit = await units.GetForUpdateAsync(cmd.Id, ct);
        switch (cmd.Action)
        {
            case UnitAction.Reserve: unit.Reserve(cmd.Until); break;
            case UnitAction.ReleaseReservation: unit.ReleaseReservation(Today); break;
            case UnitAction.MakeUnavailable: unit.MakeUnavailable(cmd.Reason ?? ""); break;
            case UnitAction.MakeAvailable:
                if (await workOrders.AnyAsync(w => w.UnitId == unit.Id && (w.Status == WorkOrderStatus.New || w.Status == WorkOrderStatus.Assigned || w.Status == WorkOrderStatus.InProgress || w.Status == WorkOrderStatus.OnHold), ct))
                    throw new ConflictException("Close or cancel the open work orders of this unit first.");
                unit.MakeAvailable(Today); break;
            default: throw new FieldValidationException("action", "Unknown unit action.");
        }
        await units.SaveChangesAsync(ct);
        return UnitDto.From(unit, Today, await fields.ReadAsync(Docs.Unit, unit, ct));
    }

    public async Task<IReadOnlyList<UnitDto>> Handle(BulkCreateUnits cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var property = await properties.FindAsync(i.PropertyId, ct) ?? throw new FieldValidationException("propertyId", "Property not found.");
        var type = await unitTypes.FindAsync(i.UnitTypeId, ct) ?? throw new FieldValidationException("unitTypeId", "Unit type not found.");

        // Unit numbers: floor and a 2-digit position ("1406" = floor 14, unit 06); ground floor 0 gives "001".."008".
        var numbers = new List<(string Number, int Floor)>();
        for (var floor = i.FromFloor; floor <= i.ToFloor; floor++)
            for (var n = 1; n <= i.UnitsPerFloor; n++)
                numbers.Add(($"{i.Prefix?.Trim()}{floor}{n:00}", floor));

        var wanted = numbers.Select(x => x.Number).ToList();
        var existing = await units.Query(new Specification<Unit>(u => u.PropertyId == property.Id && wanted.Contains(u.UnitNumber))).Select(u => u.UnitNumber).ToListAsync(ct);
        if (existing.Count > 0) throw new ConflictException($"These units already exist: {string.Join(", ", existing.Take(10))}{(existing.Count > 10 ? "..." : "")}.");

        var created = new List<Unit>();
        foreach (var (number, floor) in numbers)
        {
            var unit = Unit.Create(Today);
            unit.PropertyId = property.Id;
            unit.LegalEntityId = property.LegalEntityId;
            unit.UnitNumber = number; unit.UnitTypeId = type.Id; unit.Usage = type.DefaultUsage; unit.Bedrooms = type.DefaultBedrooms;
            unit.Floor = floor.ToString(); unit.AreaSqft = i.AreaSqft; unit.AskingRent = i.AskingRent;
            units.Add(unit);
            if (unit.AskingRent > 0) AddRate(unit, "Initial asking rent");
            created.Add(unit);
        }
        await GrowTotalUnitsAsync(property.Id, created.Count, ct);
        await units.SaveChangesAsync(ct);
        return created.Select(u => UnitDto.From(u, Today, new JsonObject())).ToList();
    }

    /// <summary>Keeps the invariant "total lettable units >= registered units" (spec RE-PROP-04) when units are added.</summary>
    private async Task GrowTotalUnitsAsync(Guid propertyId, int adding, CancellationToken ct)
    {
        var registered = await units.CountAsync(u => u.PropertyId == propertyId, ct) + adding;
        var property = await properties.GetForUpdateAsync(propertyId, ct);
        if (registered > property.TotalUnits) property.TotalUnits = registered;
    }

    private void Map(UnitInput i, Unit unit, UnitType type, string number)
    {
        unit.UnitNumber = number; unit.UnitTypeId = type.Id;
        unit.Usage = i.Usage ?? type.DefaultUsage; // usage drives the default VAT treatment of leases
        unit.Floor = i.Floor; unit.Bedrooms = i.Bedrooms ?? type.DefaultBedrooms; unit.AreaSqft = i.AreaSqft; unit.AskingRent = i.AskingRent;
        unit.Notes = i.Notes;
    }

    private void AddRate(Unit unit, string reason)
        => rates.Add(new UnitRentRate { UnitId = unit.Id, LegalEntityId = unit.LegalEntityId, EffectiveFrom = Today, AnnualRent = unit.AskingRent, Reason = reason });

    /// <summary>One rate per day: a second change on the same day replaces the first (unique per unit and date).</summary>
    private async Task RecordRateAsync(Unit unit, CancellationToken ct)
    {
        var today = Today;
        var sameDay = await rates.Tracked(new Specification<UnitRentRate>(r => r.UnitId == unit.Id && r.EffectiveFrom == today)).FirstOrDefaultAsync(ct);
        if (sameDay is null) AddRate(unit, "Asking rent changed");
        else sameDay.AnnualRent = unit.AskingRent;
    }
}
