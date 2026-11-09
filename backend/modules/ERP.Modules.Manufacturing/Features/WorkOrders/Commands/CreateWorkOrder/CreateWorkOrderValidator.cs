using FluentValidation;

namespace ERP.Modules.Manufacturing.Features;

public class CreateWorkOrderValidator : AbstractValidator<CreateWorkOrderCommand>
{
    public CreateWorkOrderValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.BomId).NotEmpty();
        RuleFor(x => x.PlannedQuantity).GreaterThan(0);
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.StartDate);
    }
}
