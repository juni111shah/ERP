using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Purchasing.Features;

public sealed class CreatePurchaseReturnValidator : AbstractValidator<CreatePurchaseReturnCommand>
{
    public CreatePurchaseReturnValidator()
    {
        RuleFor(x => x.Input).Must(i => i.PurchaseOrderId is not null || i.BillId is not null).WithName("purchaseOrderId").WithMessage("Raise the return against a purchase order or a bill.");
        RuleFor(x => x.Input.ReturnDate).MustBePlausibleDate();
        RuleFor(x => x.Input.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Input.Notes).MaximumLength(1000);
        RuleFor(x => x.Input.Lines).NotEmpty().WithMessage("A return needs at least one line.");
        RuleFor(x => x.Input.Lines).Must(l => l is null || l.Select(x => (x.OrderLineId, x.BillLineId)).Distinct().Count() == l.Count).WithMessage("A line can appear only once.");
        RuleForEach(x => x.Input.Lines).ChildRules(l => l.RuleFor(x => x.Quantity).GreaterThan(0).MustHaveAtMostDecimals(4));
    }
}
