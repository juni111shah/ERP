using FluentValidation;

namespace ERP.Modules.Pos.Features;

public class RecordDrawerTransactionValidator : AbstractValidator<RecordDrawerTransactionCommand>
{
    public RecordDrawerTransactionValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(250);
    }
}
