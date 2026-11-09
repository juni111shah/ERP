using FluentValidation;

namespace ERP.Modules.Purchasing.Features;

public sealed class UpdatePurchaseOrderValidator : AbstractValidator<UpdatePurchaseOrderCommand>
{
    public UpdatePurchaseOrderValidator() => RuleFor(x => x.Input).SetValidator(new PurchaseOrderInputValidator());
}
