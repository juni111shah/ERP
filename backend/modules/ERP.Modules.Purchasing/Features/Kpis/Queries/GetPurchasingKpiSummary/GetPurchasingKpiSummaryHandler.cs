using ERP.Application.Common;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class GetPurchasingKpiSummaryHandler(IPurchaseOrderRepository orders, IPurchaseRequisitionRepository requisitions)
    : IQueryHandler<GetPurchasingKpiSummaryQuery, PurchasingKpiDto>
{
    public async Task<PurchasingKpiDto> Handle(GetPurchasingKpiSummaryQuery q, CancellationToken ct)
    {
        // 1. Orders: all, spend, open and waiting for goods.
        var totalOrders = await orders.CountAsync(ct: ct);
        var totalSpend = await orders.TotalNotCancelledAsync(ct);

        var openOrdersCount = await orders.CountOpenAsync(ct);
        var openOrdersAmount = await orders.TotalOpenAsync(ct);

        var pendingReceipts = await orders.CountAsync(o => o.Status == PurchaseOrderStatus.Approved
            || o.Status == PurchaseOrderStatus.PartiallyReceived, ct);

        // 2. Requisitions still to approve.
        var openRequisitions = await requisitions.CountAsync(r => r.Status == PurchaseRequisitionStatus.Draft
            || r.Status == PurchaseRequisitionStatus.PendingApproval, ct);

        return new PurchasingKpiDto(
            TotalSpend: Math.Round(totalSpend, 2),
            OpenOrdersAmount: Math.Round(openOrdersAmount, 2),
            OpenOrdersCount: openOrdersCount,
            PendingReceiptsCount: pendingReceipts,
            OpenRequisitionsCount: openRequisitions,
            TotalOrdersCount: totalOrders);
    }
}
