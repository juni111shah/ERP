using FluentValidation;

namespace ERP.Modules.IndustryPacks.Features;

public sealed class TransitionRecordValidator : AbstractValidator<TransitionRecordCommand>
{
    public TransitionRecordValidator()
    {
        RuleFor(x => x.To).NotEmpty().WithMessage("Choose the next status.");
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}
