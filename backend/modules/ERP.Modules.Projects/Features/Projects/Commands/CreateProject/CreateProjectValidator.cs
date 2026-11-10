using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator() => RuleFor(x => x.Input).SetValidator(new ProjectInputValidator());
}
