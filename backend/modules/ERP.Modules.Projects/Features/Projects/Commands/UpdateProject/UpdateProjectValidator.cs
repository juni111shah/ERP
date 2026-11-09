using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class UpdateProjectValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectValidator() => RuleFor(x => x.Input).SetValidator(new ProjectInputValidator());
}
