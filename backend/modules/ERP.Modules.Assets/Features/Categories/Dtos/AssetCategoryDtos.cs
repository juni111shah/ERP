using System.Linq.Expressions;
using ERP.Modules.Assets.Domain;

namespace ERP.Modules.Assets.Features;

public sealed record AssetCategoryInput(
    string Code, string Name, int LifeMonths, DepreciationMethod Method, Guid AssetAccountId, Guid AccumulatedDepreciationAccountId, Guid DepreciationExpenseAccountId,
    decimal? DecliningRatePercent = null, Guid? DisposalGainLossAccountId = null, bool IsActive = true);

public sealed record AssetCategoryDto(
    Guid Id, string Code, string Name, int LifeMonths, DepreciationMethod Method, decimal? DecliningRatePercent, Guid AssetAccountId, Guid AccumulatedDepreciationAccountId,
    Guid DepreciationExpenseAccountId, Guid? DisposalGainLossAccountId, bool IsActive)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<AssetCategory, AssetCategoryDto>> Projection =
        c => new AssetCategoryDto(c.Id, c.Code, c.Name, c.LifeMonths, c.Method, c.DecliningRatePercent, c.AssetAccountId, c.AccumulatedDepreciationAccountId, c.DepreciationExpenseAccountId, c.DisposalGainLossAccountId, c.IsActive);

    public static AssetCategoryDto From(AssetCategory c) => new(c.Id, c.Code, c.Name, c.LifeMonths, c.Method, c.DecliningRatePercent, c.AssetAccountId, c.AccumulatedDepreciationAccountId, c.DepreciationExpenseAccountId, c.DisposalGainLossAccountId, c.IsActive);
}
