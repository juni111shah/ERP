using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class UpdateChangeOrderValidator : AbstractValidator<UpdateChangeOrderCommand>
{
    public UpdateChangeOrderValidator() => RuleFor(x => x.Input).SetValidator(new ChangeOrderInputValidator());
}
