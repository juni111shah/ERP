using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Purchasing.Features;

public sealed class GoodsReceiptInputValidator : AbstractValidator<GoodsReceiptInput>
{
    public GoodsReceiptInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.ReceivedBy).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ReceiptDate).MustBePlausibleDate();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Goods receipt must have at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty();
            l.RuleFor(x => x.QuantityReceived).GreaterThan(0).MustHaveAtMostDecimals(4);
        });
    }
}
