using FluentValidation;

namespace ERP.Application.Finance;

public sealed class BounceChequeValidator : AbstractValidator<BounceChequeCommand>
{
    public BounceChequeValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
}
