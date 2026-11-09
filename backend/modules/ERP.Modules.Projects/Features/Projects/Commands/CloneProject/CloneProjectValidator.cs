using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class CloneProjectValidator : AbstractValidator<CloneProjectCommand>
{
    public CloneProjectValidator() { RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200); }
}
