using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class SetTaskDependenciesValidator : AbstractValidator<SetTaskDependenciesCommand>
{
    public SetTaskDependenciesValidator() => RuleFor(x => x.DependsOn).Must(d => d.Count <= 50).WithMessage("At most 50 dependencies.");
}
