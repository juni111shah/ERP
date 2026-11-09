using FluentValidation;

namespace ERP.Application.Security;

public sealed class InviteUserValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserValidator()
    {
        RuleFor(x => x.Input.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Input.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Input.RoleIds).NotNull();
    }
}
