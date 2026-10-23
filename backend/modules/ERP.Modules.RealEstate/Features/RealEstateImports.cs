using ERP.Application.Abstractions;
using ERP.Application.Imports;
using ERP.Domain.Organizations;
using ERP.Modules.RealEstate.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Properties from CSV.</summary>
internal sealed class PropertyImport(ISender sender, IReadRepository<LegalEntity> entities) : IImportDefinition
{
    public string Key => "realestate.properties";
    public string Label => "Properties";
    public string? ModuleKey => RealEstateModule.Key;
    public string Permission => P.PropertiesManage;
    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        new("name", true, "Marina Heights Tower"), new("propertyType", true, "Residential", "Residential, Villa, Commercial, MixedUse, Retail, Industrial, Land"),
        new("legalEntity", false, "", "Code or name; optional when the workspace has one legal entity"), new("region", false, "Dubai"), new("community", false, "Dubai Marina"),
        new("totalUnits", false, "40"), new("indicativeValue", false, "42000000"),
    ];

    public async Task ImportRowAsync(ImportRow r, CancellationToken ct)
    {
        var entity = await ResolveEntityAsync(r, ct);
        await sender.Send(new CreateProperty(new PropertyInput(entity, r.Require("name"), r.Enum<PropertyType>("propertyType"), OwnershipType.Owned, null, r.Get("region"), r.Get("community"), null, null,
            r.Int("totalUnits") ?? 0, r.Decimal("indicativeValue") ?? 0)), ct);
    }

    private async Task<Guid> ResolveEntityAsync(ImportRow r, CancellationToken ct)
    {
        var all = await entities.Query().Select(e => new { e.Id, e.Code, e.Name }).ToListAsync(ct);
        if (r.Get("legalEntity") is { } key)
            return all.FirstOrDefault(e => e.Code.Equals(key, StringComparison.OrdinalIgnoreCase) || e.Name.Equals(key, StringComparison.OrdinalIgnoreCase))?.Id ?? throw new ImportRowException($"Legal entity '{key}' not found.");
        return all.Count == 1 ? all[0].Id : throw new ImportRowException("'legalEntity' is required because the workspace has several legal entities.");
    }
}

/// <summary>Units from CSV; the property is found by name or code and the unit type by code.</summary>
internal sealed class UnitImport(ISender sender, IReadRepository<Property> properties, IReadRepository<UnitType> unitTypes) : IImportDefinition
{
    public string Key => "realestate.units";
    public string Label => "Units";
    public string? ModuleKey => RealEstateModule.Key;
    public string Permission => P.UnitsManage;
    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        new("property", true, "Marina Heights Tower", "Property name or code"), new("unitNumber", true, "101"), new("unitType", true, "1BR", "Unit type code"),
        new("areaSqft", true, "780"), new("askingRent", true, "78000", "Annual"), new("floor", false, "1"), new("bedrooms", false, "1"),
    ];

    public async Task ImportRowAsync(ImportRow r, CancellationToken ct)
    {
        var key = r.Require("property");
        var property = await properties.Query(new Specification<Property>(p => p.Name == key || p.Code == key)).Select(p => p.Id).FirstOrDefaultAsync(ct);
        if (property == Guid.Empty) throw new ImportRowException($"Property '{key}' not found.");
        var typeCode = r.Require("unitType").ToUpperInvariant();
        var type = await unitTypes.Query(new Specification<UnitType>(t => t.Code == typeCode)).Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (type == Guid.Empty) throw new ImportRowException($"Unit type '{typeCode}' not found.");
        await sender.Send(new CreateUnit(new UnitInput(property, r.Require("unitNumber"), type, null, r.Get("floor"), r.Int("bedrooms"), r.Decimal("areaSqft") ?? 0, r.Decimal("askingRent") ?? 0)), ct);
    }
}
