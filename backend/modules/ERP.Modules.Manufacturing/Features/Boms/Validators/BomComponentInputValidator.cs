using FluentValidation;

namespace ERP.Modules.Manufacturing.Features;

public class BomComponentInputValidator : AbstractValidator<BomComponentInput>
{
    public BomComponentInputValidator()
    {
        RuleFor(x => x.ComponentItemId).NotEmpty();
        RuleFor(x => x.QuantityRequired).GreaterThan(0).Must(v => decimal.Round(v, 4) == v).WithMessage("Use at most 4 decimals.");
        RuleFor(x => x.ScrapPercentage).InclusiveBetween(0m, 100m);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
    }
}
