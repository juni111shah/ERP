using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class DecideTimesheetsValidator : AbstractValidator<DecideTimesheetsCommand>
{
    public DecideTimesheetsValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().Must(i => i.Count <= 500);
        RuleFor(x => x.Reason).NotEmpty().When(x => !x.Approve).WithMessage("Say why the time is rejected.");
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}
