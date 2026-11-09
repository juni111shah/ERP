using ERP.Application.Common;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class UpdateBomHandler(IBillOfMaterialsRepository boms, BomComponents components) : ICommandHandler<UpdateBomCommand, BillOfMaterialsDto>
{
    public async Task<BillOfMaterialsDto> Handle(UpdateBomCommand request, CancellationToken cancellationToken)
    {
        // 1. Load it; components must be catalog products, never the product itself, and an active BOM must not make a loop.
        var bom = await boms.GetWithComponentsForUpdateAsync(request.Id, cancellationToken);

        var catalog = await components.CatalogAsync(bom.ProducedItemId, request.Components, cancellationToken);
        if (request.Components is not null && request.Components.Any(c => c.ComponentItemId == bom.ProducedItemId))
            throw new FieldValidationException("components", "A product cannot be a component of itself.");
        if (request.IsActive)
            await components.EnsureNoCycleAsync(bom.LegalEntityId, bom.Id, bom.ProducedItemId, request.Components?.Select(c => c.ComponentItemId) ?? bom.Components.Select(c => c.ComponentItemId).ToList(), cancellationToken);

        // 2. Apply the changes; components, when sent, replace the current ones.
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
                bom.Components.Add(await components.ComponentAsync(bom, c, catalog, cancellationToken));
        }

        // 3. Save.
        await boms.SaveChangesAsync(cancellationToken);
        return BillOfMaterialsDto.From(bom);
    }
}
