using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class ChangeOrderInputValidator : AbstractValidator<ChangeOrderInput>
{
    public ChangeOrderInputValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.AmountChange).InclusiveBetween(-1_000_000_000m, 1_000_000_000m);
    }
}
