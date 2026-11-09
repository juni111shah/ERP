using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class GetBomByIdHandler(IBillOfMaterialsRepository boms, IStockPostingService stock) : IQueryHandler<GetBomByIdQuery, BillOfMaterialsDto?>
{
    public async Task<BillOfMaterialsDto?> Handle(GetBomByIdQuery request, CancellationToken cancellationToken)
    {
        var bom = await boms.FindWithComponentsAsync(request.Id, cancellationToken);
        if (bom is null) return null;

        // A no-tracking copy: lines with no typed cost show the item's current average.
        var costs = await BomStructure.CurrentCostsAsync(bom.Components, stock, null, cancellationToken);
        foreach (var c in bom.Components) c.UnitCost = costs[c.Id];
        return BillOfMaterialsDto.From(bom);
    }
}
