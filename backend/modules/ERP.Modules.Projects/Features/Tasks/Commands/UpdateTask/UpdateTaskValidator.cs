using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class UpdateTaskValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskValidator() => RuleFor(x => x.Input).SetValidator(new TaskInputValidator());
}
