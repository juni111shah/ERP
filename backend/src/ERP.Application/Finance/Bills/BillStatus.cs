using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public static class BillStatus
{
    /// <summary>Issued -> PartlyPaid -> Paid from the amount paid so far.</summary>
    public static void Refresh(Bill b)
    {
        if (b.Status is DocumentStatus.Draft or DocumentStatus.PendingApproval or DocumentStatus.Voided) return;
        b.Status = b.Paid <= 0 ? DocumentStatus.Issued : b.Paid >= b.Total ? DocumentStatus.Paid : DocumentStatus.PartlyPaid;
    }
}
