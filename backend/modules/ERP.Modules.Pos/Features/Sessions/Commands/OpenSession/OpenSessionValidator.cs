using FluentValidation;

namespace ERP.Modules.Pos.Features;

public class OpenSessionValidator : AbstractValidator<OpenSessionCommand>
{
    public OpenSessionValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.RegisterId).NotEmpty();
        RuleFor(x => x.OpeningCash).GreaterThanOrEqualTo(0);
    }
}
