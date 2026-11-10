using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class RecordPunchesValidator : AbstractValidator<RecordPunchesCommand>
{
    public RecordPunchesValidator()
    {
        RuleFor(x => x.Punches).NotEmpty().Must(p => p.Count <= 5000).WithMessage("Send at most 5000 punches at a time.");
        RuleFor(x => x.Source).MaximumLength(50);
    }
}
