using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class GetGoodsReceiptHandler(IGoodsReceiptRepository receipts, ICustomFieldService fields) : IQueryHandler<GetGoodsReceiptQuery, GoodsReceiptDto>
{
    public async Task<GoodsReceiptDto> Handle(GetGoodsReceiptQuery q, CancellationToken ct)
    {
        var grn = await receipts.FindWithLinesForUpdateAsync(q.Id, ct) ?? throw new NotFoundException($"Goods receipt '{q.Id}' not found.");
        return await GoodsReceiptDto.FromAsync(grn, fields, ct);
    }
}
