using FluentValidation;

namespace ERP.Modules.Services.Features;

public class UpdateTimeEntryValidator : AbstractValidator<UpdateTimeEntryCommand>
{
    public UpdateTimeEntryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ConsultantName).MaximumLength(150);
        RuleFor(x => x.Hours).GreaterThan(0).WithMessage("Hours must be greater than zero.");
        RuleFor(x => x.HourlyRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaskDescription).NotEmpty().MaximumLength(500);
    }
}
