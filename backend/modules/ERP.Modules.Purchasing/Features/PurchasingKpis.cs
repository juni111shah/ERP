using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Purchasing.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Features;

public sealed record PurchasingKpiDto(
    decimal TotalSpend,
    decimal OpenOrdersAmount,
    int OpenOrdersCount,
    int PendingReceiptsCount,
    int OpenRequisitionsCount,
    int TotalOrdersCount);

public sealed record GetPurchasingKpiSummary() : IQuery<PurchasingKpiDto>, IRequirePermission
{
    public string RequiredPermission => P.OrdersRead;
}

internal sealed class PurchasingKpiHandlers(
    IReadRepository<PurchaseOrder> orders,
    IReadRepository<PurchaseRequisition> requisitions) :
    IRequestHandler<GetPurchasingKpiSummary, PurchasingKpiDto>
{
    public async Task<PurchasingKpiDto> Handle(GetPurchasingKpiSummary q, CancellationToken ct)
    {
        var totalOrders = await orders.Query().CountAsync(ct);
        var totalSpend = await orders.Query().Where(o => o.Status != PurchaseOrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

        var openOrdersQuery = orders.Query().Where(o => o.Status == PurchaseOrderStatus.Draft
            || o.Status == PurchaseOrderStatus.Submitted
            || o.Status == PurchaseOrderStatus.Approved
            || o.Status == PurchaseOrderStatus.PartiallyReceived);
        var openOrdersCount = await openOrdersQuery.CountAsync(ct);
        var openOrdersAmount = await openOrdersQuery.SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

        var pendingReceipts = await orders.Query().CountAsync(o => o.Status == PurchaseOrderStatus.Approved
            || o.Status == PurchaseOrderStatus.PartiallyReceived, ct);

        var openRequisitions = await requisitions.Query().CountAsync(r => r.Status == PurchaseRequisitionStatus.Draft
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
