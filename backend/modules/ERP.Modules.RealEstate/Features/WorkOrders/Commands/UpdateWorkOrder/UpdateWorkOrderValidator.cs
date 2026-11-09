using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class UpdateWorkOrderValidator : AbstractValidator<UpdateWorkOrderCommand>
{
    public UpdateWorkOrderValidator() => RuleFor(x => x.Input).SetValidator(new WorkOrderInputValidator());
}
