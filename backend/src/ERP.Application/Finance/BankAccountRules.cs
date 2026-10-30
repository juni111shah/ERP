using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>
/// What counts as a bank or cash account for receipts, payments, cheques and bank transfers: an active, postable asset account
/// that is flagged as a bank account or carries the "bank" (or, where cash is acceptable, "cash") system role. Matches the
/// definition the cash position report already uses, so no other asset account (receivables, inventory, fixed assets) can be chosen.
/// </summary>
public static class BankAccountRules
{
    public static bool IsCashOrBank(Account a) => IsUsable(a) && (a.IsBank || a.SystemRole == "bank" || a.SystemRole == "cash");

    /// <summary>Cheques are deposited at a bank, so petty cash does not qualify.</summary>
    public static bool IsBankOnly(Account a) => IsUsable(a) && (a.IsBank || a.SystemRole == "bank");

    private static bool IsUsable(Account a) => a.Type == AccountType.Asset && a.IsPostable && a.IsActive;
}
