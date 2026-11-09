using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Assets.Features;

public sealed class AssetInputValidator : AbstractValidator<AssetInput>
{
    public AssetInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.AcquisitionDate).MustBePlausibleDate();
        RuleFor(x => x.Cost).GreaterThan(0).MustBeMoney();
        RuleFor(x => x.SalvageValue).GreaterThanOrEqualTo(0).MustBeMoney();
        RuleFor(x => x).Must(x => x.SalvageValue < x.Cost).WithName("salvageValue").WithMessage("The salvage value must be less than the cost.");
        RuleFor(x => x.LifeMonths).InclusiveBetween(1, 1200).When(x => x.LifeMonths is not null).WithMessage("The life must be between 1 and 1200 months.");
        RuleFor(x => x.Custodian).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
