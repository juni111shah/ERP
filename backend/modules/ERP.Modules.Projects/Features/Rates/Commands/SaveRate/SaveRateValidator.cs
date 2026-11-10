using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class SaveRateValidator : AbstractValidator<SaveRateCommand>
{
    public SaveRateValidator()
    {
        RuleFor(x => x.Input.BillRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.CostRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.Role).MaximumLength(100);
        RuleFor(x => x.Input).Must(i => i.UserId is null || string.IsNullOrWhiteSpace(i.Role)).WithName("role").WithMessage("A rate is for a person or for a role, not both.");
    }
}
