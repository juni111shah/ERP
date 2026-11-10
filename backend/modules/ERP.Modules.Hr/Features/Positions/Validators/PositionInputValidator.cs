using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class PositionInputValidator : AbstractValidator<PositionInput>
{
    public PositionInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Grade).MaximumLength(50);
    }
}
