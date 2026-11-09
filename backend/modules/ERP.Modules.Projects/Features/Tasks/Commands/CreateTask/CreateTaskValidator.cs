using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class CreateTaskValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskValidator() => RuleFor(x => x.Input).SetValidator(new TaskInputValidator());
}
