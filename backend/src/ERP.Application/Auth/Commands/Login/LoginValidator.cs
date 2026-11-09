using FluentValidation;

namespace ERP.Application.Auth;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email and password are required.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Email and password are required.");
    }
}
