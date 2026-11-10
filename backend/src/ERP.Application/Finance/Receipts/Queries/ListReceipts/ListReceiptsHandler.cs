using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class ListReceiptsHandler(ISettlementRepository settlements) : IQueryHandler<ListReceiptsQuery, PagedResult<ReceiptSummaryDto>>
{
    public Task<PagedResult<ReceiptSummaryDto>> Handle(ListReceiptsQuery query, CancellationToken ct)
        => settlements.PageByDirectionAsync(InstrumentDirection.Received, query.Paging, ReceiptSummaryDto.Projection, ct);
}
