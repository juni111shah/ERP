using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class AddMemberValidator : AbstractValidator<AddMemberCommand>
{
    public AddMemberValidator() { RuleFor(x => x.ProjectId).NotEmpty(); RuleFor(x => x.UserId).NotEmpty(); RuleFor(x => x.Role).MaximumLength(100); }
}
