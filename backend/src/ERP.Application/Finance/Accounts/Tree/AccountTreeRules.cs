using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Rules shared by the step-by-step chart-of-accounts panels (pure).</summary>
public static class AccountTreeRules
{
    /// <summary>An account type from the route, case-insensitive; numbers are not accepted.</summary>
    public static AccountType ParseType(string? value)
    {
        if (!Enum.TryParse<AccountType>(value, ignoreCase: true, out var type) || !Enum.IsDefined(type) || int.TryParse(value, out _))
            throw new FieldValidationException("type", "Unknown account type. Use asset, liability, equity, income or expense.");
        return type;
    }

    /// <summary>Panels list by code unless the caller sorts.</summary>
    public static PagedQuery Sorted(PagedQuery q) => string.IsNullOrWhiteSpace(q.Sort) ? q with { Sort = "code" } : q;

    /// <summary>Each node's own sub-category key (for a nested account, the panel path uses its top same-type ancestor's).</summary>
    public static PagedResult<AccountNodeDto> WithSubcategories(PagedResult<AccountNodeDto> page, IAccountSubcategoryRules rules)
        => page.Map(i => i with { Subcategory = rules.Classify(i.Type, i.Code, i.ReportingGroup, i.SystemRole) });

    /// <summary>Debit-natured types keep Dr − Cr; credit-natured types are shown positive on the credit side.</summary>
    public static decimal Natural(AccountType type, decimal debitMinusCredit)
        => type is AccountType.Asset or AccountType.Expense ? debitMinusCredit : -debitMinusCredit;
}
