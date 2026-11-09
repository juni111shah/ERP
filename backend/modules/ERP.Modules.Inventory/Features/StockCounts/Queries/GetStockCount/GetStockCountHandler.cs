using ERP.Application.Common;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class GetStockCountHandler(IStockCountRepository counts, StockCountDtoBuilder dtos) : IQueryHandler<GetStockCountQuery, StockCountDto>
{
    public async Task<StockCountDto> Handle(GetStockCountQuery q, CancellationToken ct) => await dtos.ToDtoAsync(await counts.GetWithLinesAsync(q.Id, ct), ct);
}
