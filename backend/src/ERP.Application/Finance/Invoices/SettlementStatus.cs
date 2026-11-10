using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public static class SettlementStatus
{
    /// <summary>Issued -> PartlyPaid -> Paid from the amount settled so far.</summary>
    public static void Refresh(Invoice i)
    {
        if (i.Status is DocumentStatus.Draft or DocumentStatus.Voided) return;
        i.Status = i.Paid <= 0 ? DocumentStatus.Issued : i.Paid >= i.Total ? DocumentStatus.Paid : DocumentStatus.PartlyPaid;
    }
}

