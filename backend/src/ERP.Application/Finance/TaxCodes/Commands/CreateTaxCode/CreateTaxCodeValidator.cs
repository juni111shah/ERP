using FluentValidation;

namespace ERP.Application.Finance;

public sealed class CreateTaxCodeValidator : AbstractValidator<CreateTaxCodeCommand>
{
    public CreateTaxCodeValidator() => RuleFor(x => x.Input).SetValidator(new TaxCodeInputValidator());
}
