using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Catalog;
using ERP.Modules.Manufacturing.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Manufacturing.Features;

public record BomComponentDto(
    Guid Id,
    Guid BomId,
    Guid ComponentItemId,
    string ComponentItemCode,
    string ComponentItemName,
    decimal QuantityRequired,
    string Uom,
    decimal ScrapPercentage,
    decimal UnitCost,
    decimal ExtendedCost,
    string? Notes,
    bool UseCurrentCost = false);

public record BomComponentInput(
    Guid ComponentItemId,
    string ComponentItemCode,
    string ComponentItemName,
    decimal QuantityRequired,
    string Uom,
    decimal ScrapPercentage,
    decimal? UnitCost,
    string? Notes);

public record BillOfMaterialsDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    Guid ProducedItemId,
    string ProducedItemCode,
    string ProducedItemName,
    decimal OutputQuantity,
    string Uom,
    string Version,
    decimal LaborCostPerUnit,
    decimal OverheadCostPerUnit,
    decimal TotalMaterialCost,
    decimal TotalUnitCost,
    bool IsActive,
    string? Description,
    int ComponentCount,
    List<BomComponentDto> Components,
    string? CustomFields,
    DateTimeOffset CreatedAt);

public record BillOfMaterialsSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    string ProducedItemCode,
    string ProducedItemName,
    decimal OutputQuantity,
    string Uom,
    string Version,
    decimal TotalUnitCost,
    bool IsActive,
    int ComponentCount,
    DateTimeOffset CreatedAt);

public record CreateBomCommand(
    Guid LegalEntityId,
    string Code,
    string Name,
    Guid ProducedItemId,
    string ProducedItemCode,
    string ProducedItemName,
    decimal OutputQuantity,
    string Uom,
    string Version,
    decimal LaborCostPerUnit,
    decimal OverheadCostPerUnit,
    bool IsActive,
    string? Description,
    List<BomComponentInput>? Components,
    string? CustomFields) : ICommand<BillOfMaterialsDto>;

public record UpdateBomCommand(
    Guid Id,
    string Name,
    decimal OutputQuantity,
    string Uom,
    string Version,
    decimal LaborCostPerUnit,
    decimal OverheadCostPerUnit,
    bool IsActive,
    string? Description,
    List<BomComponentInput>? Components,
    string? CustomFields) : ICommand<BillOfMaterialsDto>;

public record DeleteBomCommand(Guid Id) : ICommand<Unit>;

public record GetBomByIdQuery(Guid Id) : IQuery<BillOfMaterialsDto?>;

public class BomComponentInputValidator : AbstractValidator<BomComponentInput>
{
    public BomComponentInputValidator()
    {
        RuleFor(x => x.ComponentItemId).NotEmpty();
        RuleFor(x => x.QuantityRequired).GreaterThan(0).Must(v => decimal.Round(v, 4) == v).WithMessage("Use at most 4 decimals.");
        RuleFor(x => x.ScrapPercentage).InclusiveBetween(0m, 100m);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
    }
}

public class CreateBomValidator : AbstractValidator<CreateBomCommand>
{
    public CreateBomValidator()
    {
        RuleForEach(x => x.Components).SetValidator(new BomComponentInputValidator());
        RuleFor(x => x.Components).Must(c => c is null || c.Select(x => x.ComponentItemId).Distinct().Count() == c.Count).WithMessage("A component can appear only once.");
        RuleFor(x => x).Must(x => x.Components is null || x.Components.All(c => c.ComponentItemId != x.ProducedItemId)).WithName("Components").WithMessage("A product cannot be a component of itself.");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.ProducedItemId).NotEmpty();
        RuleFor(x => x.OutputQuantity).GreaterThan(0);
    }
}

public class UpdateBomValidator : AbstractValidator<UpdateBomCommand>
{
    public UpdateBomValidator()
    {
        RuleForEach(x => x.Components).SetValidator(new BomComponentInputValidator());
        RuleFor(x => x.Components).Must(c => c is null || c.Select(x => x.ComponentItemId).Distinct().Count() == c.Count).WithMessage("A component can appear only once.");
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.OutputQuantity).GreaterThan(0);
    }
}

public class BomHandlers :
    IRequestHandler<CreateBomCommand, BillOfMaterialsDto>,
    IRequestHandler<UpdateBomCommand, BillOfMaterialsDto>,
    IRequestHandler<DeleteBomCommand, Unit>,
    IRequestHandler<GetBomByIdQuery, BillOfMaterialsDto?>
{
    private readonly IRepository<BillOfMaterials> _boms;
    private readonly IReadRepository<Item> _items;
    private readonly ILegalEntityGuard _guard;
    private readonly IReadRepository<WorkOrder> _workOrders;
    private readonly IStockPostingService _stock;

    public BomHandlers(IRepository<BillOfMaterials> boms, IReadRepository<Item> items, ILegalEntityGuard guard, IReadRepository<WorkOrder> workOrders, IStockPostingService stock)
    {
        _stock = stock;
        _workOrders = workOrders;
        _boms = boms;
        _items = items;
        _guard = guard;
    }

    /// <summary>Catalog rows (sku, name) of the produced item and every component; each must exist and be a product. Names and codes come from the catalog, never from the request.</summary>
    private async Task<Dictionary<Guid, (string Sku, string Name, decimal PurchasePrice)>> CatalogAsync(Guid producedItemId, IEnumerable<BomComponentInput>? components, CancellationToken ct)
    {
        var ids = (components ?? []).Select(c => c.ComponentItemId).Append(producedItemId).Distinct().ToList();
        var found = await _items.Query(new Specification<Item>(i => ids.Contains(i.Id) && i.Type == ItemType.Product)).Select(i => new { i.Id, i.Sku, i.Name, i.PurchasePrice }).ToListAsync(ct);
        var map = found.ToDictionary(i => i.Id, i => (i.Sku, i.Name, i.PurchasePrice ?? 0m));
        if (!map.ContainsKey(producedItemId)) throw new FieldValidationException("producedItemId", "Choose a product item to produce.");
        if (components is not null && components.Any(c => !map.ContainsKey(c.ComponentItemId))) throw new FieldValidationException("components", "A component was not found or is not a product.");
        return map;
    }

    /// <summary>The typed cost, or with none typed the item's current average (its purchase price before anything is in stock). Such a line keeps following the average on every read.</summary>
    private async Task<BomComponent> ComponentAsync(BillOfMaterials bom, BomComponentInput c, IReadOnlyDictionary<Guid, (string Sku, string Name, decimal PurchasePrice)> catalog, CancellationToken ct)
    {
        var useCurrent = c.UnitCost is null or <= 0;
        var cost = c.UnitCost ?? 0m;
        if (useCurrent)
        {
            var average = await _stock.GetAverageCostAsync(c.ComponentItemId, null, ct);
            cost = average > 0 ? average : catalog[c.ComponentItemId].PurchasePrice;
        }
        return new BomComponent
        {
            LegalEntityId = bom.LegalEntityId,
            BomId = bom.Id,
            ComponentItemId = c.ComponentItemId,
            ComponentItemCode = catalog[c.ComponentItemId].Sku,
            ComponentItemName = catalog[c.ComponentItemId].Name,
            QuantityRequired = c.QuantityRequired,
            Uom = string.IsNullOrWhiteSpace(c.Uom) ? "PCS" : c.Uom.Trim(),
            ScrapPercentage = c.ScrapPercentage,
            UnitCost = cost,
            UseCurrentCost = useCurrent,
            Notes = c.Notes?.Trim()
        };
    }

    /// <summary>Refuses a BOM that would make its product, however many levels down, a component of itself. Only active BOMs count; reactivating one comes back through here.</summary>
    private async Task EnsureNoCycleAsync(Guid legalEntityId, Guid? bomId, Guid producedItemId, IEnumerable<Guid> componentIds, CancellationToken ct)
    {
        var others = await _boms.Query(new Specification<BillOfMaterials>(b => b.LegalEntityId == legalEntityId && b.IsActive && b.Id != bomId))
            .Include(b => b.Components).ToListAsync(ct);
        var edges = new Dictionary<Guid, HashSet<Guid>>();
        var codes = new Dictionary<Guid, string>();
        foreach (var other in others)
        {
            if (!edges.TryGetValue(other.ProducedItemId, out var set)) edges[other.ProducedItemId] = set = [];
            codes[other.ProducedItemId] = other.ProducedItemCode;
            foreach (var c in other.Components) { set.Add(c.ComponentItemId); codes[c.ComponentItemId] = c.ComponentItemCode; }
        }

        foreach (var component in componentIds.Distinct())
        {
            if (BomStructure.FindPath(edges, component, producedItemId) is { } path)
                throw new FieldValidationException("components", $"This would make a loop: {string.Join(" uses ", path.Select(i => codes.GetValueOrDefault(i, i.ToString())))}, which this BOM would then use again.");
        }
    }

    public async Task<BillOfMaterialsDto> Handle(CreateBomCommand request, CancellationToken cancellationToken)
    {
        await _guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _boms.AnyAsync(b => b.Code == code, cancellationToken)) throw new ConflictException($"A BOM with code '{code}' already exists.");
        var catalog = await CatalogAsync(request.ProducedItemId, request.Components, cancellationToken);
        if (request.IsActive && request.Components is not null)
            await EnsureNoCycleAsync(request.LegalEntityId, null, request.ProducedItemId, request.Components.Select(c => c.ComponentItemId), cancellationToken);
        var bom = new BillOfMaterials
        {
            LegalEntityId = request.LegalEntityId,
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            ProducedItemId = request.ProducedItemId,
            ProducedItemCode = catalog[request.ProducedItemId].Sku,
            ProducedItemName = catalog[request.ProducedItemId].Name,
            OutputQuantity = request.OutputQuantity,
            Uom = string.IsNullOrWhiteSpace(request.Uom) ? "PCS" : request.Uom.Trim(),
            Version = string.IsNullOrWhiteSpace(request.Version) ? "1.0" : request.Version.Trim(),
            LaborCostPerUnit = request.LaborCostPerUnit,
            OverheadCostPerUnit = request.OverheadCostPerUnit,
            IsActive = request.IsActive,
            Description = request.Description?.Trim(),
            CustomFields = request.CustomFields
        };

        if (request.Components != null)
        {
            foreach (var c in request.Components)
                bom.Components.Add(await ComponentAsync(bom, c, catalog, cancellationToken));
        }

        _boms.Add(bom);
        await _boms.SaveChangesAsync(cancellationToken);
        return Map(bom);
    }

    public async Task<BillOfMaterialsDto> Handle(UpdateBomCommand request, CancellationToken cancellationToken)
    {
        var bom = await _boms.Tracked(new Specification<BillOfMaterials>(b => b.Id == request.Id).Include(b => b.Components))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"BOM {request.Id} not found.");

        var catalog = await CatalogAsync(bom.ProducedItemId, request.Components, cancellationToken);
        if (request.Components is not null && request.Components.Any(c => c.ComponentItemId == bom.ProducedItemId))
            throw new FieldValidationException("components", "A product cannot be a component of itself.");
        if (request.IsActive)
            await EnsureNoCycleAsync(bom.LegalEntityId, bom.Id, bom.ProducedItemId, request.Components?.Select(c => c.ComponentItemId) ?? bom.Components.Select(c => c.ComponentItemId).ToList(), cancellationToken);
        bom.Name = request.Name.Trim();
        bom.OutputQuantity = request.OutputQuantity;
        bom.Uom = string.IsNullOrWhiteSpace(request.Uom) ? "PCS" : request.Uom.Trim();
        bom.Version = string.IsNullOrWhiteSpace(request.Version) ? "1.0" : request.Version.Trim();
        bom.LaborCostPerUnit = request.LaborCostPerUnit;
        bom.OverheadCostPerUnit = request.OverheadCostPerUnit;
        bom.IsActive = request.IsActive;
        bom.Description = request.Description?.Trim();
        bom.CustomFields = request.CustomFields;

        if (request.Components != null)
        {
            bom.Components.Clear();
            foreach (var c in request.Components)
                bom.Components.Add(await ComponentAsync(bom, c, catalog, cancellationToken));
        }

        await _boms.SaveChangesAsync(cancellationToken);
        return Map(bom);
    }

    public async Task<Unit> Handle(DeleteBomCommand request, CancellationToken cancellationToken)
    {
        var bom = await _boms.GetForUpdateAsync(request.Id, cancellationToken);
        if (await _workOrders.AnyAsync(w => w.BomId == bom.Id, cancellationToken))
            throw new ConflictException("This BOM is used by work orders and cannot be deleted. Deactivate it instead.");
        _boms.Remove(bom);
        await _boms.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    public async Task<BillOfMaterialsDto?> Handle(GetBomByIdQuery request, CancellationToken cancellationToken)
    {
        var bom = await _boms.Query(new Specification<BillOfMaterials>(b => b.Id == request.Id))
            .Include(b => b.Components)
            .FirstOrDefaultAsync(cancellationToken);
        if (bom is null) return null;

        // A no-tracking copy: lines with no typed cost show the item's current average.
        var costs = await BomStructure.CurrentCostsAsync(bom.Components, _stock, null, cancellationToken);
        foreach (var c in bom.Components) c.UnitCost = costs[c.Id];
        return Map(bom);
    }

    private static BillOfMaterialsDto Map(BillOfMaterials b)
    {
        var totalMaterial = b.Components.Sum(c => c.QuantityRequired * c.UnitCost * (1 + (c.ScrapPercentage / 100m)));
        var totalUnitCost = b.OutputQuantity > 0
            ? Math.Round((totalMaterial / b.OutputQuantity) + b.LaborCostPerUnit + b.OverheadCostPerUnit, 2)
            : 0;

        return new BillOfMaterialsDto(
            b.Id,
            b.LegalEntityId,
            b.Code,
            b.Name,
            b.ProducedItemId,
            b.ProducedItemCode,
            b.ProducedItemName,
            b.OutputQuantity,
            b.Uom,
            b.Version,
            b.LaborCostPerUnit,
            b.OverheadCostPerUnit,
            totalMaterial,
            totalUnitCost,
            b.IsActive,
            b.Description,
            b.Components.Count,
            b.Components.Select(c => new BomComponentDto(
                c.Id,
                c.BomId,
                c.ComponentItemId,
                c.ComponentItemCode,
                c.ComponentItemName,
                c.QuantityRequired,
                c.Uom,
                c.ScrapPercentage,
                c.UnitCost,
                Math.Round(c.QuantityRequired * c.UnitCost * (1 + (c.ScrapPercentage / 100m)), 2),
                c.Notes,
                c.UseCurrentCost)).ToList(),
            b.CustomFields,
            b.CreatedAt);
    }
}
