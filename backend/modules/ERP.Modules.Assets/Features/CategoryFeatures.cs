using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modules.Assets.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Assets.Features;

public sealed record AssetCategoryInput(
    string Code, string Name, int LifeMonths, DepreciationMethod Method, Guid AssetAccountId, Guid AccumulatedDepreciationAccountId, Guid DepreciationExpenseAccountId,
    decimal? DecliningRatePercent = null, Guid? DisposalGainLossAccountId = null, bool IsActive = true);

public sealed record AssetCategoryDto(
    Guid Id, string Code, string Name, int LifeMonths, DepreciationMethod Method, decimal? DecliningRatePercent, Guid AssetAccountId, Guid AccumulatedDepreciationAccountId,
    Guid DepreciationExpenseAccountId, Guid? DisposalGainLossAccountId, bool IsActive);

public sealed record GetAssetCategory(Guid Id) : IQuery<AssetCategoryDto>, IRequirePermission { public string RequiredPermission => Permissions.CategoriesRead; }
public sealed record CreateAssetCategory(AssetCategoryInput Input) : ICommand<AssetCategoryDto>, IRequirePermission { public string RequiredPermission => Permissions.CategoriesManage; }
public sealed record UpdateAssetCategory(Guid Id, AssetCategoryInput Input) : ICommand<AssetCategoryDto>, IRequirePermission { public string RequiredPermission => Permissions.CategoriesManage; }
public sealed record DeleteAssetCategory(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => Permissions.CategoriesManage; }

public sealed class AssetCategoryInputValidator : AbstractValidator<AssetCategoryInput>
{
    public AssetCategoryInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.LifeMonths).InclusiveBetween(1, 1200).WithMessage("The life must be between 1 and 1200 months.");
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.DecliningRatePercent).InclusiveBetween(0.01m, 100m).When(x => x.DecliningRatePercent is not null).WithMessage("The annual rate must be above 0 and at most 100 percent.");
        RuleFor(x => x.AssetAccountId).NotEmpty();
        RuleFor(x => x.AccumulatedDepreciationAccountId).NotEmpty();
        RuleFor(x => x.DepreciationExpenseAccountId).NotEmpty();
    }
}
public sealed class CreateAssetCategoryValidator : AbstractValidator<CreateAssetCategory> { public CreateAssetCategoryValidator() => RuleFor(x => x.Input).SetValidator(new AssetCategoryInputValidator()); }
public sealed class UpdateAssetCategoryValidator : AbstractValidator<UpdateAssetCategory> { public UpdateAssetCategoryValidator() => RuleFor(x => x.Input).SetValidator(new AssetCategoryInputValidator()); }

internal sealed class AssetCategoryHandlers(IRepository<AssetCategory> categories, IReadRepository<FixedAsset> assets, IReadRepository<Account> accounts) :
    IRequestHandler<GetAssetCategory, AssetCategoryDto>,
    IRequestHandler<CreateAssetCategory, AssetCategoryDto>,
    IRequestHandler<UpdateAssetCategory, AssetCategoryDto>,
    IRequestHandler<DeleteAssetCategory, Unit>
{
    public async Task<AssetCategoryDto> Handle(GetAssetCategory q, CancellationToken ct) => ToDto(await categories.GetAsync(q.Id, ct));

    public async Task<AssetCategoryDto> Handle(CreateAssetCategory cmd, CancellationToken ct)
    {
        var category = new AssetCategory();
        await ApplyAsync(category, cmd.Input, ct);
        categories.Add(category);
        await categories.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<AssetCategoryDto> Handle(UpdateAssetCategory cmd, CancellationToken ct)
    {
        var category = await categories.GetForUpdateAsync(cmd.Id, ct);
        await ApplyAsync(category, cmd.Input, ct);
        await categories.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<Unit> Handle(DeleteAssetCategory cmd, CancellationToken ct)
    {
        var category = await categories.GetForUpdateAsync(cmd.Id, ct);
        if (await assets.AnyAsync(a => a.CategoryId == category.Id, ct)) throw new ConflictException("This category has assets. Make it inactive instead.");
        categories.Remove(category);
        await categories.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private async Task ApplyAsync(AssetCategory c, AssetCategoryInput i, CancellationToken ct)
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

    private static AssetCategoryDto ToDto(AssetCategory c) => new(c.Id, c.Code, c.Name, c.LifeMonths, c.Method, c.DecliningRatePercent, c.AssetAccountId, c.AccumulatedDepreciationAccountId, c.DepreciationExpenseAccountId, c.DisposalGainLossAccountId, c.IsActive);
}
