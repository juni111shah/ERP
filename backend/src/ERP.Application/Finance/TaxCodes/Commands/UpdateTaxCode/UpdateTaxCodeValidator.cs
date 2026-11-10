using FluentValidation;

namespace ERP.Application.Finance;

public sealed class UpdateTaxCodeValidator : AbstractValidator<UpdateTaxCodeCommand>
{
    public UpdateTaxCodeValidator() => RuleFor(x => x.Input).SetValidator(new TaxCodeInputValidator());
}
