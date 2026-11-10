using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetReceiptByIdHandler(ISettlementRepository settlements, ReceiptDtoBuilder dtos) : IQueryHandler<GetReceiptByIdQuery, ReceiptDto>
{
    public async Task<ReceiptDto> Handle(GetReceiptByIdQuery query, CancellationToken ct)
    {
        var s = await settlements.GetReceiptWithAllocationsAsync(query.Id, ct);
        return await dtos.BuildAsync(s, ct);
    }
}
