using FluentValidation;

namespace ERP.Modules.Assets.Features;

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
