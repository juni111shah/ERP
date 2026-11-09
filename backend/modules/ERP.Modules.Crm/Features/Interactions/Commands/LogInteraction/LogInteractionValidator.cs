using FluentValidation;

namespace ERP.Modules.Crm.Features;

public class LogInteractionValidator : AbstractValidator<LogInteractionCommand>
{
    public LogInteractionValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.RelatedEntityKind).NotEmpty().Must(k => InteractionKinds.All.Contains(k?.Trim() ?? "", StringComparer.OrdinalIgnoreCase))
            .WithMessage("The related record must be a Lead, Opportunity or Customer.");
        RuleFor(x => x.RelatedEntityId).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
    }
}
