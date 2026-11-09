using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class HireCandidateValidator : AbstractValidator<HireCandidateCommand>
{
    public HireCandidateValidator()
    {
        RuleFor(x => x.Input.LegalEntityId).NotEmpty();
        RuleFor(x => x.Input.Designation).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Input.BasicSalary).GreaterThan(0);
    }
}
