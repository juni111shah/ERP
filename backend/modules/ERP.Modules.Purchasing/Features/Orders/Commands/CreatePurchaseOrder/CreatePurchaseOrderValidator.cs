using FluentValidation;

namespace ERP.Modules.Purchasing.Features;

public sealed class CreatePurchaseOrderValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderValidator() => RuleFor(x => x.Input).SetValidator(new PurchaseOrderInputValidator());
}
