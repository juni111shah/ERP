using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Purchasing.Features;

public sealed class ConvertPurchaseOrderToBillValidator : AbstractValidator<ConvertPurchaseOrderToBillCommand>
{
    public ConvertPurchaseOrderToBillValidator()
    {
        RuleFor(x => x.UnitPriceOverrides).Must(PurchaseOrderBillRules.AreValidOverrides).WithMessage("Override prices cannot be negative and can have at most 4 decimal places.");
        RuleFor(x => x.VendorReference).MaximumLength(100);
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(x => x.Quantity).GreaterThan(0).MustHaveAtMostDecimals(4));
    }
}
