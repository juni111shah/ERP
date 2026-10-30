using ERP.Domain.Finance;

namespace ERP.Application.Finance;

// ---- Account sub-categories (the second level of the step-by-step chart of accounts) -------------------------------
// A generic, statement-oriented split of each account type, the same for every industry and country:
//
//   asset      -> current, noncurrent        (Current assets, Non-current assets)
//   liability  -> current, noncurrent        (Current liabilities, Non-current liabilities)
//   equity     -> equity                     (Equity)
//   income     -> operating, other           (Operating revenue, Other income)
//   expense    -> costOfSales, operating, other (Cost of sales, Operating expenses, Other expenses)
// Keys are unique within a type; the API addresses them as /tree/categories/{type}/subcategories/{key}.
//
// Sub-categories are derived, not stored: nothing in the database changes. An account is classified by, in order:
//   1. its reporting group, when it names a known sub-category ("Current assets", "Fixed assets", "Cost of sales", ...);
//   2. otherwise its code, when numeric (the common numbering convention):
//        asset / liability: second digit 0-4 current, 5-9 non-current (1000-1499 / 2000-2499 current);
//        income: second digit 0-7 operating, 8-9 other (4800-4999 other income);
//        expense: first digit 5 cost of sales, 8-9 other, anything else operating;
//   3. otherwise current (assets, liabilities), equity, operating revenue or operating expenses.
// Only primary accounts (no parent, or a parent of another type) are classified for the panels; their sub-accounts stay
// under them and open through the children endpoint. A module or country pack can replace IAccountSubcategoryRules.

/// <summary>One derived sub-category of an account type. <c>Key</c> is stable and unique within the type ("current"); <c>Name</c> is the English label.</summary>
public sealed record AccountSubcategory(string Key, AccountType Type, string Name, int Order);

/// <summary>Classifies accounts into sub-categories. Replaceable (register another implementation) without touching the core.</summary>
public interface IAccountSubcategoryRules
{
    /// <summary>The sub-categories of one type, in display order. Never empty.</summary>
    IReadOnlyList<AccountSubcategory> For(AccountType type);

    /// <summary>The key of the sub-category an account of <paramref name="type"/> belongs to; always one of <see cref="For"/>.</summary>
    string Classify(AccountType type, string code, string? reportingGroup, string? systemRole);
}

/// <summary>The default, generic rule set described above.</summary>
public sealed class StandardAccountSubcategoryRules : IAccountSubcategoryRules
{
    public const string Current = "current", NonCurrent = "noncurrent", EquityAll = "equity";
    public const string Operating = "operating", Other = "other", CostOfSales = "costOfSales";

    private static readonly IReadOnlyDictionary<AccountType, IReadOnlyList<AccountSubcategory>> Sets = new Dictionary<AccountType, IReadOnlyList<AccountSubcategory>>
    {
        [AccountType.Asset] = [new(Current, AccountType.Asset, "Current assets", 1), new(NonCurrent, AccountType.Asset, "Non-current assets", 2)],
        [AccountType.Liability] = [new(Current, AccountType.Liability, "Current liabilities", 1), new(NonCurrent, AccountType.Liability, "Non-current liabilities", 2)],
        [AccountType.Equity] = [new(EquityAll, AccountType.Equity, "Equity", 1)],
        [AccountType.Income] = [new(Operating, AccountType.Income, "Operating revenue", 1), new(Other, AccountType.Income, "Other income", 2)],
        [AccountType.Expense] =
        [
            new(CostOfSales, AccountType.Expense, "Cost of sales", 1), new(Operating, AccountType.Expense, "Operating expenses", 2),
            new(Other, AccountType.Expense, "Other expenses", 3),
        ],
    };

    /// <summary>Reporting-group names (lower case, punctuation-free) that map straight to a sub-category.</summary>
    private static readonly IReadOnlyDictionary<string, (AccountType Type, string Key)> Groups = new Dictionary<string, (AccountType, string)>
    {
        ["current assets"] = (AccountType.Asset, Current), ["current asset"] = (AccountType.Asset, Current),
        ["non current assets"] = (AccountType.Asset, NonCurrent), ["noncurrent assets"] = (AccountType.Asset, NonCurrent), ["non current asset"] = (AccountType.Asset, NonCurrent),
        ["fixed assets"] = (AccountType.Asset, NonCurrent), ["fixed asset"] = (AccountType.Asset, NonCurrent), ["long term assets"] = (AccountType.Asset, NonCurrent),
        ["current liabilities"] = (AccountType.Liability, Current), ["current liability"] = (AccountType.Liability, Current),
        ["non current liabilities"] = (AccountType.Liability, NonCurrent), ["noncurrent liabilities"] = (AccountType.Liability, NonCurrent),
        ["non current liability"] = (AccountType.Liability, NonCurrent), ["long term liabilities"] = (AccountType.Liability, NonCurrent),
        ["equity"] = (AccountType.Equity, EquityAll),
        ["revenue"] = (AccountType.Income, Operating), ["operating revenue"] = (AccountType.Income, Operating), ["sales"] = (AccountType.Income, Operating),
        ["operating income"] = (AccountType.Income, Operating),
        ["other income"] = (AccountType.Income, Other), ["other revenue"] = (AccountType.Income, Other), ["non operating income"] = (AccountType.Income, Other),
        ["cost of sales"] = (AccountType.Expense, CostOfSales), ["cost of goods sold"] = (AccountType.Expense, CostOfSales),
        ["direct costs"] = (AccountType.Expense, CostOfSales), ["cost of revenue"] = (AccountType.Expense, CostOfSales),
        ["operating expenses"] = (AccountType.Expense, Operating), ["operating expense"] = (AccountType.Expense, Operating),
        ["administrative expenses"] = (AccountType.Expense, Operating),
        ["other expenses"] = (AccountType.Expense, Other), ["other expense"] = (AccountType.Expense, Other), ["finance costs"] = (AccountType.Expense, Other),
        ["non operating expenses"] = (AccountType.Expense, Other),
    };

    public IReadOnlyList<AccountSubcategory> For(AccountType type) => Sets[type];

    public string Classify(AccountType type, string code, string? reportingGroup, string? systemRole)
    {
        if (Normalize(reportingGroup) is { } group && Groups.TryGetValue(group, out var hit) && hit.Type == type) return hit.Key;
        var digits = (code ?? string.Empty).Trim();
        if (digits.Length >= 2 && char.IsAsciiDigit(digits[0]) && char.IsAsciiDigit(digits[1]))
        {
            int first = digits[0] - '0', second = digits[1] - '0';
            switch (type)
            {
                case AccountType.Asset or AccountType.Liability: return second <= 4 ? Current : NonCurrent;
                case AccountType.Income: return second <= 7 ? Operating : Other;
                case AccountType.Expense: return first == 5 ? CostOfSales : first >= 8 ? Other : Operating;
            }
        }
        return Sets[type][type == AccountType.Expense ? 1 : 0].Key;
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var chars = value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
