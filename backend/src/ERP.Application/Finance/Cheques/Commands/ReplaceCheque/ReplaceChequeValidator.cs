using FluentValidation;

namespace ERP.Application.Finance;

public sealed class ReplaceChequeValidator : AbstractValidator<ReplaceChequeCommand>
{
    public ReplaceChequeValidator() => RuleFor(x => x.Replacement).SetValidator(new ChequeInputValidator());
}
