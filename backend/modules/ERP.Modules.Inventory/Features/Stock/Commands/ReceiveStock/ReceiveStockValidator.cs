using FluentValidation;

namespace ERP.Modules.Inventory.Features;

public sealed class ReceiveStockValidator : AbstractValidator<ReceiveStockCommand>
{
    public ReceiveStockValidator()
    {
        RuleFor(x => x.Input.Quantity).GreaterThan(0).FourDecimals(); RuleFor(x => x.Input.UnitCost).GreaterThanOrEqualTo(0).FourDecimals();
        RuleFor(x => x.Input.WarehouseId).NotEmpty(); RuleFor(x => x.Input.ItemId).NotEmpty(); RuleFor(x => x.Input.OffsetAccountId).NotEmpty();
        RuleFor(x => x.Input.Date).ValidStockDate();
    }
}
