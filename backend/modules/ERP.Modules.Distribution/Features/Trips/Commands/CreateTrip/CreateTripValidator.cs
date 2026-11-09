using FluentValidation;

namespace ERP.Modules.Distribution.Features;

public class CreateTripValidator : AbstractValidator<CreateTripCommand>
{
    public CreateTripValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.RouteId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one inventory item must be loaded for van sales.");
        RuleFor(x => x.DriverName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.VehicleNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Items).Must(i => i is null || i.Select(x => x.ItemId).Distinct().Count() == i.Count).WithMessage("An item can be loaded only once.");
        RuleForEach(x => x.Items).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty();
            l.RuleFor(x => x.LoadedQuantity).GreaterThan(0);
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}
