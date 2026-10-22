using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using MediatR;

namespace ERP.Application.Finance;

/// <summary>Receipts and payments share one table; each list only shows its own direction. Search, filter, sort and paging stay generic.</summary>
public sealed record ListReceipts(PagedQuery Paging) : IQuery<PagedResult<ReceiptSummaryDto>>, IRequirePermission { public string RequiredPermission => CorePermissions.ReceiptsRead; }
public sealed record ListPayments(PagedQuery Paging) : IQuery<PagedResult<PaymentSummaryDto>>, IRequirePermission { public string RequiredPermission => CorePermissions.PaymentsRead; }

internal sealed class SettlementListHandlers(IReadRepository<Settlement> settlements) :
    IRequestHandler<ListReceipts, PagedResult<ReceiptSummaryDto>>,
    IRequestHandler<ListPayments, PagedResult<PaymentSummaryDto>>
{
    public Task<PagedResult<ReceiptSummaryDto>> Handle(ListReceipts q, CancellationToken ct)
        => settlements.PageAsync(q.Paging, r => new ReceiptSummaryDto(r.Id, r.Number, r.PartyId, r.PartyName, r.Date, r.Method, r.Amount, r.Unallocated, r.Status, r.CurrencyCode),
            new Specification<Settlement>(s => s.Direction == InstrumentDirection.Received), ct);

    public Task<PagedResult<PaymentSummaryDto>> Handle(ListPayments q, CancellationToken ct)
        => settlements.PageAsync(q.Paging, p => new PaymentSummaryDto(p.Id, p.Number, p.PartyId, p.PartyName, p.Date, p.Method, p.Amount, p.Unallocated, p.Status, p.CurrencyCode),
            new Specification<Settlement>(s => s.Direction == InstrumentDirection.Issued), ct);
}
