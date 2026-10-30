using ERP.Application.Common;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Features;

public static class PurchasingTransitions
{
    public static readonly StatusTransitions<PurchaseOrderStatus> Order = new(
        new Dictionary<PurchaseOrderStatus, IEnumerable<PurchaseOrderStatus>>
        {
            [PurchaseOrderStatus.Draft] = [PurchaseOrderStatus.Submitted, PurchaseOrderStatus.Approved, PurchaseOrderStatus.Cancelled],
            [PurchaseOrderStatus.Submitted] = [PurchaseOrderStatus.Approved, PurchaseOrderStatus.Cancelled, PurchaseOrderStatus.Draft],
            [PurchaseOrderStatus.Approved] = [PurchaseOrderStatus.PartiallyReceived, PurchaseOrderStatus.Received, PurchaseOrderStatus.Cancelled, PurchaseOrderStatus.Billed],
            [PurchaseOrderStatus.PartiallyReceived] = [PurchaseOrderStatus.Received, PurchaseOrderStatus.Billed],
            [PurchaseOrderStatus.Received] = [PurchaseOrderStatus.Billed],
            [PurchaseOrderStatus.Billed] = [],
            [PurchaseOrderStatus.Cancelled] = [],
        },
        systemOnly: [PurchaseOrderStatus.Billed]);

    public static readonly StatusTransitions<PurchaseRequisitionStatus> Requisition = new(
        new Dictionary<PurchaseRequisitionStatus, IEnumerable<PurchaseRequisitionStatus>>
        {
            [PurchaseRequisitionStatus.Draft] = [PurchaseRequisitionStatus.PendingApproval, PurchaseRequisitionStatus.Approved, PurchaseRequisitionStatus.Rejected],
            [PurchaseRequisitionStatus.PendingApproval] = [PurchaseRequisitionStatus.Approved, PurchaseRequisitionStatus.Rejected],
            [PurchaseRequisitionStatus.Approved] = [PurchaseRequisitionStatus.Ordered],
            [PurchaseRequisitionStatus.Rejected] = [PurchaseRequisitionStatus.Draft],
            [PurchaseRequisitionStatus.Ordered] = [],
        },
        systemOnly: [PurchaseRequisitionStatus.Ordered]);
}
