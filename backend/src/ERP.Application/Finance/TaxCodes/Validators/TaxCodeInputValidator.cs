using FluentValidation;

namespace ERP.Application.Finance;

public sealed class TaxCodeInputValidator : AbstractValidator<TaxCodeInput>
{
    public TaxCodeInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Rate).InclusiveBetween(0, 100).WithMessage("The rate is a percentage between 0 and 100.");
        RuleFor(x => x).Must(x => !x.IsDefault || x.IsActive).WithName("isDefault").WithMessage("The default tax code must be active.");
    }
}
