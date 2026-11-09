using FluentValidation;

namespace ERP.Modules.Purchasing.Features;

public sealed class CreateGoodsReceiptValidator : AbstractValidator<CreateGoodsReceiptCommand>
{
    public CreateGoodsReceiptValidator() => RuleFor(x => x.Input).SetValidator(new GoodsReceiptInputValidator());
}
