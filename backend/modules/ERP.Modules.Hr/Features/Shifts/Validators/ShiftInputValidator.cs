using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class ShiftInputValidator : AbstractValidator<ShiftInput>
{
    public ShiftInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GraceMinutes).InclusiveBetween(0, 240);
        RuleFor(x => x.HoursPerDay).InclusiveBetween(1, 24);
    }
}
