using ERP.Application.Abstractions;
using ERP.Application.Common;
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
    string? Notes);

public record BomComponentInput(
    Guid ComponentItemId,
    string ComponentItemCode,
    string ComponentItemName,
    decimal QuantityRequired,
    string Uom,
    decimal ScrapPercentage,
    decimal UnitCost,
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

public class CreateBomValidator : AbstractValidator<CreateBomCommand>
{
    public CreateBomValidator()
    {
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

    public BomHandlers(IRepository<BillOfMaterials> boms)
    {
        _boms = boms;
    }

    public async Task<BillOfMaterialsDto> Handle(CreateBomCommand request, CancellationToken cancellationToken)
    {
        var bom = new BillOfMaterials
        {
            LegalEntityId = request.LegalEntityId,
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            ProducedItemId = request.ProducedItemId,
            ProducedItemCode = request.ProducedItemCode.Trim(),
            ProducedItemName = request.ProducedItemName.Trim(),
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
            {
                bom.Components.Add(new BomComponent
                {
                    LegalEntityId = bom.LegalEntityId,
                    BomId = bom.Id,
                    ComponentItemId = c.ComponentItemId,
                    ComponentItemCode = c.ComponentItemCode.Trim(),
                    ComponentItemName = c.ComponentItemName.Trim(),
                    QuantityRequired = c.QuantityRequired,
                    Uom = string.IsNullOrWhiteSpace(c.Uom) ? "PCS" : c.Uom.Trim(),
                    ScrapPercentage = c.ScrapPercentage,
                    UnitCost = c.UnitCost,
                    Notes = c.Notes?.Trim()
                });
            }
        }

        _boms.Add(bom);
        await _boms.SaveChangesAsync(cancellationToken);
        return Map(bom);
    }

    public async Task<BillOfMaterialsDto> Handle(UpdateBomCommand request, CancellationToken cancellationToken)
    {
        var bom = await _boms.Query(new Specification<BillOfMaterials>(b => b.Id == request.Id))
            .Include(b => b.Components)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"BOM {request.Id} not found.");

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
            {
                bom.Components.Add(new BomComponent
                {
                    LegalEntityId = bom.LegalEntityId,
                    BomId = bom.Id,
                    ComponentItemId = c.ComponentItemId,
                    ComponentItemCode = c.ComponentItemCode.Trim(),
                    ComponentItemName = c.ComponentItemName.Trim(),
                    QuantityRequired = c.QuantityRequired,
                    Uom = string.IsNullOrWhiteSpace(c.Uom) ? "PCS" : c.Uom.Trim(),
                    ScrapPercentage = c.ScrapPercentage,
                    UnitCost = c.UnitCost,
                    Notes = c.Notes?.Trim()
                });
            }
        }

        await _boms.SaveChangesAsync(cancellationToken);
        return Map(bom);
    }

    public async Task<Unit> Handle(DeleteBomCommand request, CancellationToken cancellationToken)
    {
        var bom = await _boms.GetForUpdateAsync(request.Id, cancellationToken);
        _boms.Remove(bom);
        await _boms.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    public async Task<BillOfMaterialsDto?> Handle(GetBomByIdQuery request, CancellationToken cancellationToken)
    {
        var bom = await _boms.Query(new Specification<BillOfMaterials>(b => b.Id == request.Id))
            .Include(b => b.Components)
            .FirstOrDefaultAsync(cancellationToken);

        return bom is null ? null : Map(bom);
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
                c.Notes)).ToList(),
            b.CustomFields,
            b.CreatedAt);
    }
}
