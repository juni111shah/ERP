using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class BulkCreateUnitsValidator : AbstractValidator<BulkCreateUnitsCommand>
{
    public const int MaxUnits = 500;

    public BulkCreateUnitsValidator()
    {
        RuleFor(x => x.Input.PropertyId).NotEmpty();
        RuleFor(x => x.Input.UnitTypeId).NotEmpty();
        RuleFor(x => x.Input.FromFloor).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.ToFloor).GreaterThanOrEqualTo(x => x.Input.FromFloor);
        RuleFor(x => x.Input.UnitsPerFloor).InclusiveBetween(1, 99);
        RuleFor(x => x.Input.AreaSqft).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.AskingRent).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => (x.Input.ToFloor - x.Input.FromFloor + 1) * x.Input.UnitsPerFloor <= MaxUnits)
            .WithName("input").WithMessage($"Create at most {MaxUnits} units at a time.");
    }
}
