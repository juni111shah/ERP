using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Modules.Assets.Domain;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

/// <summary>Checks and applies a category's input (create and update): a unique code and active, postable accounts of the right types.</summary>
internal sealed class AssetCategoryRules(IAssetCategoryRepository categories, IReadRepository<Account> accounts)
{
    public async Task ApplyAsync(AssetCategory c, AssetCategoryInput i, CancellationToken ct)
    {
        var code = i.Code.Trim();
        if (await categories.AnyAsync(x => x.Code == code && x.Id != c.Id, ct)) throw new ConflictException($"An asset category with code '{code}' already exists.");
        if (i.AssetAccountId == i.AccumulatedDepreciationAccountId) throw new FieldValidationException("accumulatedDepreciationAccountId", "Cost and accumulated depreciation need separate accounts.");
        await RequireAccountAsync(i.AssetAccountId, "assetAccountId", [AccountType.Asset], "an asset", ct);
        await RequireAccountAsync(i.AccumulatedDepreciationAccountId, "accumulatedDepreciationAccountId", [AccountType.Asset], "an asset (contra)", ct);
        await RequireAccountAsync(i.DepreciationExpenseAccountId, "depreciationExpenseAccountId", [AccountType.Expense], "an expense", ct);
        if (i.DisposalGainLossAccountId is { } gainLoss) await RequireAccountAsync(gainLoss, "disposalGainLossAccountId", [AccountType.Income, AccountType.Expense], "an income or expense", ct);
        c.Code = code; c.Name = i.Name.Trim(); c.LifeMonths = i.LifeMonths; c.Method = i.Method; c.DecliningRatePercent = i.DecliningRatePercent; c.AssetAccountId = i.AssetAccountId;
        c.AccumulatedDepreciationAccountId = i.AccumulatedDepreciationAccountId; c.DepreciationExpenseAccountId = i.DepreciationExpenseAccountId; c.DisposalGainLossAccountId = i.DisposalGainLossAccountId; c.IsActive = i.IsActive;
    }

    private async Task RequireAccountAsync(Guid id, string field, AccountType[] types, string label, CancellationToken ct)
    {
        var account = await accounts.FindAsync(id, ct);
        if (account is null || !account.IsActive || !account.IsPostable || !types.Contains(account.Type))
            throw new FieldValidationException(field, $"Choose an active, postable {label} account.");
    }
}
