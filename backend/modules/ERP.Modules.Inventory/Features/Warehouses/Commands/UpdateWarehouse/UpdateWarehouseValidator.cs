using FluentValidation;

namespace ERP.Modules.Inventory;

public sealed class UpdateWarehouseValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseValidator() => RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200).WithMessage("Code and name are required.");
}
