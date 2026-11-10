using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Features;

/// <summary>The accounts that hold owner money. Workspaces created before the owner payable existed get them on first use, so billing a managed property never stops for a missing account.</summary>
internal sealed class OwnerAccounts(IRepository<Account> accounts)
{
    public const string PayableRole = "realestate.owner.payable";
    public const string FeeIncomeRole = "realestate.income.management";
    public const string RechargeRole = "realestate.income.recharges";

    public Task<Guid> PayableAsync(CancellationToken ct) => EnsureAsync(PayableRole, "2210", "Owner funds payable", AccountType.Liability, "Current liabilities", ct);
    public Task<Guid> FeeIncomeAsync(CancellationToken ct) => EnsureAsync(FeeIncomeRole, "4020", "Management fee income", AccountType.Income, "Revenue", ct);
    public Task<Guid> RechargeAsync(CancellationToken ct) => EnsureAsync(RechargeRole, "4030", "Maintenance recharged to owners", AccountType.Income, "Other income", ct);

    private async Task<Guid> EnsureAsync(string role, string code, string name, AccountType type, string group, CancellationToken ct)
    {
        var id = await accounts.Query(new Specification<Account>(a => a.SystemRole == role && a.IsActive)).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
        if (id is { } found) return found;
        var free = code;
        while (await accounts.AnyAsync(a => a.Code == free, ct)) free = (int.Parse(free) + 1).ToString();
        var account = new Account { Code = free, Name = name, Type = type, SystemRole = role, ReportingGroup = group, IsSystem = true, IsPostable = true, IsActive = true };
        accounts.Add(account);
        await accounts.SaveChangesAsync(ct); // the posting service reads accounts from the database
        return account.Id;
    }
}
