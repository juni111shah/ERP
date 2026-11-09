using ERP.Modules.RealEstate.Domain;
using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class UpdateVendorWorkOrderValidator : AbstractValidator<UpdateVendorWorkOrderCommand>
{
    public UpdateVendorWorkOrderValidator()
    {
        RuleFor(x => x.Update.Status).Must(s => s is WorkOrderStatus.InProgress or WorkOrderStatus.OnHold or WorkOrderStatus.Completed).WithMessage("You can set an order to in progress, on hold or completed.");
        RuleFor(x => x.Update.ActualCost).GreaterThanOrEqualTo(0).When(x => x.Update.ActualCost is not null);
        RuleFor(x => x.Update.Notes).MaximumLength(4000);
    }
}
