using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class CreateChangeOrderValidator : AbstractValidator<CreateChangeOrderCommand>
{
    public CreateChangeOrderValidator() => RuleFor(x => x.Input).SetValidator(new ChangeOrderInputValidator());
}
