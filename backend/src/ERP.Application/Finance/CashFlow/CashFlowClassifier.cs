using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>
/// Which section an account's cash belongs to, from its type and reporting group (pure). Equity is financing. System accounts (receivables, payables, tax, inventory,
/// payroll...) are the operating cycle whatever their group says. Otherwise non-current asset groups are investing, non-current liability and borrowing groups are
/// financing, and everything else is operating.
/// </summary>
public static class CashFlowClassifier
{
    public static CashFlowCategory Classify(AccountType type, string? reportingGroup, string? systemRole)
    {
        if (type == AccountType.Equity) return CashFlowCategory.Financing;
        if (systemRole is not null) return CashFlowCategory.Operating;
        var group = (reportingGroup ?? "").ToLowerInvariant();
        if (type == AccountType.Asset && (group.Contains("non-current") || group.Contains("noncurrent") || group.Contains("fixed asset") || group.Contains("investment"))) return CashFlowCategory.Investing;
        if (type == AccountType.Liability && (group.Contains("non-current") || group.Contains("noncurrent") || group.Contains("loan") || group.Contains("borrowing"))) return CashFlowCategory.Financing;
        return CashFlowCategory.Operating;
    }
}
