using FluentValidation;

namespace ERP.Modules.Sales.Features;

public sealed class UpdateQuoteValidator : AbstractValidator<UpdateQuoteCommand>
{
    public UpdateQuoteValidator() => RuleFor(x => x.Input).SetValidator(new QuoteInputValidator());
}
