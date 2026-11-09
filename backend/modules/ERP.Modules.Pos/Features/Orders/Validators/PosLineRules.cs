using FluentValidation;

namespace ERP.Modules.Pos.Features;

/// <summary>The line rules shared by sale, quote and hold.</summary>
internal static class PosLineRules
{
    public static void Lines(InlineValidator<PosOrderLineInput> l)
    {
        l.RuleFor(x => x.Quantity).GreaterThan(0);
        l.RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100);
        l.When(x => x.TaxRate.HasValue, () =>
        {
            l.RuleFor(x => x.TaxRate!.Value).InclusiveBetween(0, 100).WithMessage("Tax rate must be between 0 and 100.");
        });
        l.When(x => !x.ItemId.HasValue, () =>
        {
            l.RuleFor(x => x.ItemCode).NotEmpty();
            l.RuleFor(x => x.UnitPrice).NotNull().WithMessage("A free-text line needs a unit price.");
        });
        l.When(x => x.UnitPrice.HasValue, () =>
        {
            l.RuleFor(x => x.UnitPrice!.Value).GreaterThanOrEqualTo(0);
        });
    }
}
