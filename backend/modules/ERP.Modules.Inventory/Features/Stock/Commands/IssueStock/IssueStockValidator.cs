using FluentValidation;

namespace ERP.Modules.Inventory.Features;

public sealed class IssueStockValidator : AbstractValidator<IssueStockCommand>
{
    public IssueStockValidator()
    {
        RuleFor(x => x.Input.Quantity).GreaterThan(0).FourDecimals(); RuleFor(x => x.Input.WarehouseId).NotEmpty(); RuleFor(x => x.Input.ItemId).NotEmpty();
        RuleFor(x => x.Input.Date).ValidStockDate();
    }
}
