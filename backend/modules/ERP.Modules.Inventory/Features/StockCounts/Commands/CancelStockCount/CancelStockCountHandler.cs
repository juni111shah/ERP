using ERP.Application.Common;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class CancelStockCountHandler(IStockCountRepository counts, StockCountDtoBuilder dtos) : ICommandHandler<CancelStockCountCommand, StockCountDto>
{
    public async Task<StockCountDto> Handle(CancelStockCountCommand cmd, CancellationToken ct)
    {
        var count = await counts.GetWithLinesForUpdateAsync(cmd.Id, ct);
        if (count.Status == StockCountStatus.Posted) throw new ConflictException("A posted count cannot be cancelled.");
        count.Status = StockCountStatus.Cancelled;
        await counts.SaveChangesAsync(ct);
        return await dtos.ToDtoAsync(count, ct);
    }
}
