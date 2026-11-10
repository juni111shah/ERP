using FluentValidation;

namespace ERP.Modules.Assets.Features;

public sealed class RunDepreciationValidator : AbstractValidator<RunDepreciationCommand>
{
    public RunDepreciationValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}
