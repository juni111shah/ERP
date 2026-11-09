using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class ListPaymentsHandler(ISettlementRepository settlements) : IQueryHandler<ListPaymentsQuery, PagedResult<PaymentSummaryDto>>
{
    public Task<PagedResult<PaymentSummaryDto>> Handle(ListPaymentsQuery query, CancellationToken ct)
        => settlements.PageByDirectionAsync(InstrumentDirection.Issued, query.Paging, PaymentSummaryDto.Projection, ct);
}
