using FluentValidation;

namespace ERP.Modules.Purchasing.Features;

public sealed class CreateRequisitionValidator : AbstractValidator<CreateRequisitionCommand>
{
    public CreateRequisitionValidator() => RuleFor(x => x.Input).SetValidator(new RequisitionInputValidator());
}
