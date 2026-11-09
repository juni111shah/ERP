using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class CreateWorkOrderValidator : AbstractValidator<CreateWorkOrderCommand>
{
    public CreateWorkOrderValidator() => RuleFor(x => x.Input).SetValidator(new WorkOrderInputValidator());
}
