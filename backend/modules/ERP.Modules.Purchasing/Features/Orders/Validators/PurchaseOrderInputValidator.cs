using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Purchasing.Features;

public sealed class PurchaseOrderInputValidator : AbstractValidator<PurchaseOrderInput>
{
    public PurchaseOrderInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.VendorName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(10);
        RuleFor(x => x.OrderDate).MustBePlausibleDate();
        RuleFor(x => x.ExpectedDeliveryDate).MustBePlausibleDate();
        RuleFor(x => x).Must(x => x.ExpectedDeliveryDate is null || x.ExpectedDeliveryDate >= x.OrderDate).WithName("expectedDeliveryDate")
            .WithMessage("The expected delivery date cannot be before the order date.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Purchase order must contain at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
            l.RuleFor(x => x.Quantity).GreaterThan(0).MustHaveAtMostDecimals(4);
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).MustHaveAtMostDecimals(4);
            l.RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100);
            l.RuleFor(x => x.TaxRate).InclusiveBetween(0, 100);
        });
    }
}
