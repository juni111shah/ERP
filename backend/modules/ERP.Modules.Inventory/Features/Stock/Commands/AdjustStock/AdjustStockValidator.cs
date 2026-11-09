using FluentValidation;

namespace ERP.Modules.Inventory.Features;

public sealed class AdjustStockValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockValidator()
    {
        RuleFor(x => x.Input.QuantityDelta).NotEqual(0).FourDecimals(); RuleFor(x => x.Input.UnitCost).GreaterThanOrEqualTo(0).FourDecimals();
        RuleFor(x => x.Input.WarehouseId).NotEmpty(); RuleFor(x => x.Input.ItemId).NotEmpty();
        RuleFor(x => x.Input.Date).ValidStockDate();
    }
}
