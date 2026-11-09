using FluentValidation;

namespace ERP.Modules.Pos.Features;

public class CreatePosReturnValidator : AbstractValidator<CreatePosReturnCommand>
{
    public CreatePosReturnValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Say why the goods came back.").MaximumLength(500);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Choose at least one item to return.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.OrderLineId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
        });
        RuleFor(x => x.Refunds).NotEmpty().WithMessage("Say how the money goes back.");
        RuleForEach(x => x.Refunds).ChildRules(r =>
        {
            r.RuleFor(x => x.Amount).GreaterThan(0);
            r.RuleFor(x => x.Method).IsInEnum();
            r.RuleFor(x => x.Reference).MaximumLength(100);
        });
    }
}
