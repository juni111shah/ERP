using FluentValidation;

namespace ERP.Modules.Manufacturing.Features;

internal static class MaterialLineRules
{
    public static void Apply(InlineValidator<MaterialLineInput> l)
    {
        l.RuleFor(x => x.ItemId).NotEmpty();
        l.RuleFor(x => x.Quantity).GreaterThan(0).Must(v => decimal.Round(v, 4) == v).WithMessage("Use at most 4 decimals.");
        l.RuleFor(x => x.Notes).MaximumLength(500);
    }
}
