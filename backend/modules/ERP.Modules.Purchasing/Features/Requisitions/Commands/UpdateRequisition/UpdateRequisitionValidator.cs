using FluentValidation;

namespace ERP.Modules.Purchasing.Features;

public sealed class UpdateRequisitionValidator : AbstractValidator<UpdateRequisitionCommand>
{
    public UpdateRequisitionValidator() => RuleFor(x => x.Input).SetValidator(new RequisitionInputValidator());
}
