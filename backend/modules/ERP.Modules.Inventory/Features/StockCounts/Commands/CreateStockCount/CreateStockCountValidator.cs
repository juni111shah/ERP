using FluentValidation;

namespace ERP.Modules.Inventory.Features;

public sealed class CreateStockCountValidator : AbstractValidator<CreateStockCountCommand>
{
    public CreateStockCountValidator() { RuleFor(x => x.Input.WarehouseId).NotEmpty(); RuleFor(x => x.Input.Memo).MaximumLength(500); }
}
