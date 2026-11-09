using FluentValidation;

namespace ERP.Modules.Services.Features;

public class LogTimeEntryValidator : AbstractValidator<LogTimeEntryCommand>
{
    public LogTimeEntryValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty().WithMessage("Legal entity is required.");
        RuleFor(x => x.EngagementId).NotEmpty().WithMessage("Engagement is required.");
        RuleFor(x => x.ConsultantName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Hours).GreaterThan(0).WithMessage("Hours must be greater than zero.");
        RuleFor(x => x.HourlyRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaskDescription).NotEmpty().MaximumLength(500);
    }
}
