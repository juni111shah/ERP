using FluentValidation;

namespace ERP.Modules.Inventory;

public sealed class CreateWarehouseValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseValidator()
    {
        RuleFor(x => x.Input.Code).NotEmpty().MaximumLength(20).WithMessage("Code and name are required.");
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200).WithMessage("Code and name are required.");
    }
}
