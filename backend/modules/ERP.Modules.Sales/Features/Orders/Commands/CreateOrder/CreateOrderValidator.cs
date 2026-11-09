using FluentValidation;

namespace ERP.Modules.Sales.Features;

public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator() => RuleFor(x => x.Input).SetValidator(new OrderInputValidator());
}
