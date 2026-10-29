using ERP.Application.Finance;
using ERP.Domain.Finance;

namespace ERP.UnitTests;

public class BankAccountRulesTests
{
    private static Account Acc(AccountType type = AccountType.Asset, bool bank = false, string? role = null, bool postable = true, bool active = true)
        => new() { Type = type, IsBank = bank, SystemRole = role, IsPostable = postable, IsActive = active, Code = "1", Name = "n" };

    [Fact] public void A_flagged_bank_account_qualifies() => Assert.True(BankAccountRules.IsCashOrBank(Acc(bank: true)));
    [Fact] public void The_cash_role_qualifies_for_receipts_but_not_for_cheque_deposits()
    {
        Assert.True(BankAccountRules.IsCashOrBank(Acc(role: "cash")));
        Assert.False(BankAccountRules.IsBankOnly(Acc(role: "cash")));
    }
    [Fact] public void Receivables_inventory_and_other_assets_do_not_qualify()
    {
        Assert.False(BankAccountRules.IsCashOrBank(Acc(role: "ar.control")));
        Assert.False(BankAccountRules.IsCashOrBank(Acc()));
    }
    [Fact] public void Inactive_header_or_non_asset_accounts_do_not_qualify()
    {
        Assert.False(BankAccountRules.IsCashOrBank(Acc(bank: true, active: false)));
        Assert.False(BankAccountRules.IsCashOrBank(Acc(bank: true, postable: false)));
        Assert.False(BankAccountRules.IsCashOrBank(Acc(AccountType.Expense, bank: true)));
    }
}
