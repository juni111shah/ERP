using FluentValidation;

namespace ERP.Modules.Sales.Features;

public sealed class CreateQuoteValidator : AbstractValidator<CreateQuoteCommand>
{
    public CreateQuoteValidator() => RuleFor(x => x.Input).SetValidator(new QuoteInputValidator());
}
