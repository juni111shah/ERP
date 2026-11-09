using FluentValidation;

namespace ERP.Modules.Inventory.Features;

public sealed class TransferStockValidator : AbstractValidator<TransferStockCommand>
{
    public TransferStockValidator()
    {
        RuleFor(x => x.Input.Quantity).GreaterThan(0).FourDecimals();
        RuleFor(x => x.Input.FromWarehouseId).NotEmpty(); RuleFor(x => x.Input.ToWarehouseId).NotEmpty(); RuleFor(x => x.Input.ItemId).NotEmpty();
        RuleFor(x => x.Input.ToWarehouseId).NotEqual(x => x.Input.FromWarehouseId).WithMessage("Choose two different warehouses.");
        RuleFor(x => x.Input.Date).ValidStockDate();
    }
}
