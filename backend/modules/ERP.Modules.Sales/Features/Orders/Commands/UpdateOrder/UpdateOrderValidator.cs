using FluentValidation;

namespace ERP.Modules.Sales.Features;

public sealed class UpdateOrderValidator : AbstractValidator<UpdateOrderCommand>
{
    public UpdateOrderValidator() => RuleFor(x => x.Input).SetValidator(new OrderInputValidator());
}
