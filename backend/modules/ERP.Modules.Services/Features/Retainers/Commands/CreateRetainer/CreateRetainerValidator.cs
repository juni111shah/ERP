using FluentValidation;

namespace ERP.Modules.Services.Features;

public class CreateRetainerValidator : AbstractValidator<CreateRetainerCommand>
{
    public CreateRetainerValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty().WithMessage("Legal entity is required.");
        RuleFor(x => x.EngagementId).NotEmpty().WithMessage("Engagement is required.");
        RuleFor(x => x.ClientId).NotEmpty().WithMessage("Client is required.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RecurringFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IncludedHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OverageHourlyRate).GreaterThanOrEqualTo(0);
    }
}
