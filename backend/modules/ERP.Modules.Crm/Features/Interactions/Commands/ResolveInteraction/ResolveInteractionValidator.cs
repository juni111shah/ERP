using FluentValidation;

namespace ERP.Modules.Crm.Features;

public class ResolveInteractionValidator : AbstractValidator<ResolveInteractionCommand>
{
    public ResolveInteractionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
