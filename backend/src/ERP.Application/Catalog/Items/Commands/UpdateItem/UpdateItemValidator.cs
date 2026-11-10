using FluentValidation;

namespace ERP.Application.Catalog;

public sealed class UpdateItemValidator : AbstractValidator<UpdateItemCommand>
{
    public UpdateItemValidator()
    {
        RuleFor(x => x.Input.Sku).NotEmpty().MaximumLength(50).WithMessage("SKU and name are required.");
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200).WithMessage("SKU and name are required.");
        RuleFor(x => x.Input.Barcode).MaximumLength(64);
    }
}
