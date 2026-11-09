using FluentValidation;

namespace ERP.Modules.Inventory.Features;

public sealed class SetStockPolicyValidator : AbstractValidator<SetStockPolicyCommand>
{
    public SetStockPolicyValidator() { RuleFor(x => x.Input.ItemId).NotEmpty(); RuleFor(x => x.Input.ReorderLevel).GreaterThanOrEqualTo(0); RuleFor(x => x.Input.ReorderQuantity).GreaterThanOrEqualTo(0); }
}
