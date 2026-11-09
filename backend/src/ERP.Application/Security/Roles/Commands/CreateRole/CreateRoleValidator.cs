using FluentValidation;

namespace ERP.Application.Security;

public sealed class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(100).WithMessage("Name is required.");
        RuleFor(x => x.Input.Permissions).NotNull();
    }
}
