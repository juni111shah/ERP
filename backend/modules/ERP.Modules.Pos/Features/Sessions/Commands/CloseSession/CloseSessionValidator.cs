using FluentValidation;

namespace ERP.Modules.Pos.Features;

public class CloseSessionValidator : AbstractValidator<CloseSessionCommand>
{
    public CloseSessionValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ActualCash).GreaterThanOrEqualTo(0);
    }
}
