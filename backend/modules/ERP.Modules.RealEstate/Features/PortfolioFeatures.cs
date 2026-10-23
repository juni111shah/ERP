using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

// ---- Unit types ---------------------------------------------------------------------------------------------------

public sealed record UnitTypeInput(string Code, string Name, UnitUsage DefaultUsage, int? DefaultBedrooms, bool IsActive = true);

public sealed record UnitTypeDto(Guid Id, string Code, string Name, UnitUsage DefaultUsage, int? DefaultBedrooms, bool IsActive)
{
    public static UnitTypeDto From(UnitType t) => new(t.Id, t.Code, t.Name, t.DefaultUsage, t.DefaultBedrooms, t.IsActive);
}

public sealed record CreateUnitType(UnitTypeInput Input) : ICommand<UnitTypeDto>, IRequirePermission
{
    public string RequiredPermission => P.UnitTypesManage;
}

public sealed record UpdateUnitType(Guid Id, UnitTypeInput Input) : ICommand<UnitTypeDto>, IRequirePermission
{
    public string RequiredPermission => P.UnitTypesManage;
}

public sealed class UnitTypeInputValidator : AbstractValidator<UnitTypeInput>
{
    public UnitTypeInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DefaultUsage).IsInEnum();
        RuleFor(x => x.DefaultBedrooms).InclusiveBetween(0, 20).When(x => x.DefaultBedrooms is not null);
    }
}

public sealed class CreateUnitTypeValidator : AbstractValidator<CreateUnitType>
{
    public CreateUnitTypeValidator() => RuleFor(x => x.Input).SetValidator(new UnitTypeInputValidator());
}

public sealed class UpdateUnitTypeValidator : AbstractValidator<UpdateUnitType>
{
    public UpdateUnitTypeValidator() => RuleFor(x => x.Input).SetValidator(new UnitTypeInputValidator());
}

internal sealed class UnitTypeHandlers(IRepository<UnitType> types) :
    IRequestHandler<CreateUnitType, UnitTypeDto>,
    IRequestHandler<UpdateUnitType, UnitTypeDto>
{
    public async Task<UnitTypeDto> Handle(CreateUnitType cmd, CancellationToken ct)
    {
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (await types.AnyAsync(t => t.Code == code, ct)) throw new ConflictException($"A unit type with code '{code}' exists.");
        var type = new UnitType();
        Map(cmd.Input, type, code);
        types.Add(type);
        await types.SaveChangesAsync(ct);
        return UnitTypeDto.From(type);
    }

    public async Task<UnitTypeDto> Handle(UpdateUnitType cmd, CancellationToken ct)
    {
        var type = await types.GetForUpdateAsync(cmd.Id, ct);
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (code != type.Code && await types.AnyAsync(t => t.Code == code, ct)) throw new ConflictException($"A unit type with code '{code}' exists.");
        Map(cmd.Input, type, code);
        await types.SaveChangesAsync(ct);
        return UnitTypeDto.From(type);
    }

    private static void Map(UnitTypeInput i, UnitType t, string code)
    {
        t.Code = code; t.Name = i.Name.Trim(); t.DefaultUsage = i.DefaultUsage; t.DefaultBedrooms = i.DefaultBedrooms; t.IsActive = i.IsActive;
    }
}

// ---- Properties ---------------------------------------------------------------------------------------------------

public sealed record PropertyInput(
    Guid LegalEntityId, string Name, PropertyType PropertyType, OwnershipType OwnershipType, Guid? OwnerPartyId, string? Region,
    string? Community, string? PlotNumber, int? YearBuilt, int TotalUnits, decimal IndicativeValue, bool IsActive = true,
    string? Notes = null, JsonObject? CustomFields = null, decimal ManagementFeePercent = 0);

public sealed record PropertyDto(
    Guid Id, string Code, string Name, Guid LegalEntityId, PropertyType PropertyType, OwnershipType OwnershipType, Guid? OwnerPartyId,
    string? Region, string? Community, string? PlotNumber, int? YearBuilt, int TotalUnits, decimal IndicativeValue, bool IsActive,
    string? Notes, JsonObject CustomFields, decimal ManagementFeePercent = 0)
{
    public static PropertyDto From(Property p, JsonObject customFields) => new(
        p.Id, p.Code, p.Name, p.LegalEntityId, p.PropertyType, p.OwnershipType, p.OwnerPartyId, p.Region, p.Community, p.PlotNumber,
        p.YearBuilt, p.TotalUnits, p.IndicativeValue, p.IsActive, p.Notes, customFields, p.ManagementFeePercent);
}

/// <summary>Detail view with the KPI cards of spec RE-PROP-03.</summary>
public sealed record PropertySummaryDto(
    PropertyDto Property, int RegisteredUnits, int OccupiedUnits, decimal OccupancyPercent, decimal ContractedAnnualRent,
    decimal GrossYieldPercent, bool UnitsNotFullyRegistered);

public sealed record CreateProperty(PropertyInput Input) : ICommand<PropertyDto>, IRequirePermission
{
    public string RequiredPermission => P.PropertiesManage;
}

public sealed record UpdateProperty(Guid Id, PropertyInput Input) : ICommand<PropertyDto>, IRequirePermission
{
    public string RequiredPermission => P.PropertiesManage;
}

public sealed record GetProperty(Guid Id) : IQuery<PropertySummaryDto>, IRequirePermission
{
    public string RequiredPermission => P.PropertiesRead;
}

public sealed class PropertyInputValidator : AbstractValidator<PropertyInput>
{
    public PropertyInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PropertyType).IsInEnum();
        RuleFor(x => x.OwnershipType).IsInEnum();
        RuleFor(x => x.OwnerPartyId).NotNull().When(x => x.OwnershipType == OwnershipType.ManagedForOwner)
            .WithMessage("Choose the owner of a property managed for a third party.");
        RuleFor(x => x.TotalUnits).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IndicativeValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ManagementFeePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.YearBuilt).InclusiveBetween(1800, 2200).When(x => x.YearBuilt is not null);
    }
}

public sealed class CreatePropertyValidator : AbstractValidator<CreateProperty>
{
    public CreatePropertyValidator() => RuleFor(x => x.Input).SetValidator(new PropertyInputValidator());
}

public sealed class UpdatePropertyValidator : AbstractValidator<UpdateProperty>
{
    public UpdatePropertyValidator() => RuleFor(x => x.Input).SetValidator(new PropertyInputValidator());
}

internal sealed class PropertyHandlers(
    IRepository<Property> properties, IReadRepository<Unit> units, IReadRepository<Lease> leases, IReadRepository<Party> parties,
    INumberingService numbers, ICustomFieldService fields, ILegalEntityGuard legalEntityGuard) :
    IRequestHandler<CreateProperty, PropertyDto>,
    IRequestHandler<UpdateProperty, PropertyDto>,
    IRequestHandler<GetProperty, PropertySummaryDto>
{
    public async Task<PropertyDto> Handle(CreateProperty cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        await legalEntityGuard.RequireAsync(i.LegalEntityId, ct);
        await EnsureOwnerAsync(i, ct);
        var property = new Property { Code = await numbers.NextAsync(Docs.Property, i.LegalEntityId, ct: ct), LegalEntityId = i.LegalEntityId };
        Map(i, property);
        await fields.ApplyAsync(Docs.Property, property, i.CustomFields, isCreate: true, ct);
        properties.Add(property);
        await properties.SaveChangesAsync(ct);
        return PropertyDto.From(property, await fields.ReadAsync(Docs.Property, property, ct));
    }

    public async Task<PropertyDto> Handle(UpdateProperty cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var property = await properties.GetForUpdateAsync(cmd.Id, ct);
        var registered = await units.CountAsync(u => u.PropertyId == property.Id, ct);
        if (i.LegalEntityId != property.LegalEntityId)
        {
            // RE-PROP-01: the owning entity cannot change once units or leases exist.
            if (registered > 0 || await leases.AnyAsync(l => l.PropertyId == property.Id, ct))
                throw new ConflictException("The owning legal entity cannot change once the property has units or leases.");
            await legalEntityGuard.RequireAsync(i.LegalEntityId, ct);
            property.LegalEntityId = i.LegalEntityId;
        }
        if (i.TotalUnits < registered) throw new FieldValidationException("totalUnits", $"Total units cannot be below the {registered} registered units.");
        await EnsureOwnerAsync(i, ct);
        Map(i, property);
        await fields.ApplyAsync(Docs.Property, property, i.CustomFields, isCreate: false, ct);
        await properties.SaveChangesAsync(ct);
        return PropertyDto.From(property, await fields.ReadAsync(Docs.Property, property, ct));
    }

    public async Task<PropertySummaryDto> Handle(GetProperty q, CancellationToken ct)
    {
        var property = await properties.GetAsync(q.Id, ct);
        var registered = await units.CountAsync(u => u.PropertyId == property.Id, ct);
        var occupied = await units.CountAsync(u => u.PropertyId == property.Id && u.Status == UnitStatus.Occupied, ct);
        // At most one occupying lease per unit, so this list is bounded; summed here because SQLite cannot aggregate decimals.
        var contracted = (await leases.Query(new Specification<Lease>(l => l.PropertyId == property.Id && (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.HoldingOver)))
            .Select(l => l.AnnualRent).ToListAsync(ct)).Sum();
        var occupancy = registered == 0 ? 0m : Math.Round(occupied * 100m / registered, 1);
        var yield = property.IndicativeValue > 0 ? Math.Round(contracted * 100m / property.IndicativeValue, 2) : 0m;
        return new(PropertyDto.From(property, await fields.ReadAsync(Docs.Property, property, ct)), registered, occupied, occupancy, contracted, yield, registered < property.TotalUnits);
    }

    private async Task EnsureOwnerAsync(PropertyInput i, CancellationToken ct)
    {
        if (i.OwnershipType != OwnershipType.ManagedForOwner) return;
        if (i.OwnerPartyId is not { } ownerId || !await parties.AnyAsync(p => p.Id == ownerId, ct))
            throw new FieldValidationException("ownerPartyId", "Owner not found.");
    }

    private static void Map(PropertyInput i, Property p)
    {
        p.Name = i.Name.Trim(); p.PropertyType = i.PropertyType; p.OwnershipType = i.OwnershipType;
        p.OwnerPartyId = i.OwnershipType == OwnershipType.ManagedForOwner ? i.OwnerPartyId : null;
        p.Region = i.Region; p.Community = i.Community; p.PlotNumber = i.PlotNumber; p.YearBuilt = i.YearBuilt;
        p.ManagementFeePercent = i.OwnershipType == OwnershipType.ManagedForOwner ? i.ManagementFeePercent : 0;
        p.TotalUnits = i.TotalUnits; p.IndicativeValue = i.IndicativeValue; p.IsActive = i.IsActive; p.Notes = i.Notes;
    }
}
