using FluentValidation;

namespace ERP.Application.Security;

public sealed class UpdateRoleValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(100).WithMessage("Name is required.");
        RuleFor(x => x.Input.Permissions).NotNull();
    }
}
