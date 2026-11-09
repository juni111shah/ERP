using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class UnitTypeInputValidator : AbstractValidator<UnitTypeInput>
{
    public UnitTypeInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DefaultUsage).IsInEnum();
        RuleFor(x => x.DefaultBedrooms).InclusiveBetween(0, 20).When(x => x.DefaultBedrooms is not null);
    }
}
