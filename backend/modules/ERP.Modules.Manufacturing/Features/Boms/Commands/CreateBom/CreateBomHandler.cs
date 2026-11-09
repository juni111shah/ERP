using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class CreateBomHandler(IBillOfMaterialsRepository boms, BomComponents components, ILegalEntityGuard guard) : ICommandHandler<CreateBomCommand, BillOfMaterialsDto>
{
    public async Task<BillOfMaterialsDto> Handle(CreateBomCommand request, CancellationToken cancellationToken)
    {
        // 1. The code is unique; the product and every component must be catalog products; an active BOM must not make a loop.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await boms.AnyAsync(b => b.Code == code, cancellationToken)) throw new ConflictException($"A BOM with code '{code}' already exists.");
        var catalog = await components.CatalogAsync(request.ProducedItemId, request.Components, cancellationToken);
        if (request.IsActive && request.Components is not null)
            await components.EnsureNoCycleAsync(request.LegalEntityId, null, request.ProducedItemId, request.Components.Select(c => c.ComponentItemId), cancellationToken);

        // 2. Build the BOM with names and codes from the catalog.
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
                bom.Components.Add(await components.ComponentAsync(bom, c, catalog, cancellationToken));
        }

        // 3. Save.
        boms.Add(bom);
        await boms.SaveChangesAsync(cancellationToken);
        return BillOfMaterialsDto.From(bom);
    }
}
