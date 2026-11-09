using ERP.Application.Finance;

namespace ERP.Modules.Purchasing.Features;

public static class PurchaseOrderBillRules
{
    /// <summary>Override prices for the bill must not be negative and carry at most four decimals.</summary>
    public static bool AreValidOverrides(IReadOnlyDictionary<Guid, decimal>? overrides)
        => overrides is null || overrides.Values.All(p => p >= 0 && MoneyValidation.HasAtMostDecimals(p, 4));
}
