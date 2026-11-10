using FluentValidation;

namespace ERP.Application.Platform;

public sealed class RegisterWorkspaceValidator : AbstractValidator<RegisterWorkspaceCommand>
{
    public RegisterWorkspaceValidator()
    {
        RuleFor(x => x.Input.BusinessName).NotEmpty().WithMessage("Enter the business name.").MaximumLength(200);
        RuleFor(x => x.Input.SignInKey).NotEmpty().WithMessage("Choose a workspace code.").Matches("^[a-zA-Z0-9][a-zA-Z0-9-]{1,48}$").WithMessage("Use 2-49 letters, digits or dashes.");
        RuleFor(x => x.Input.Industry).NotEmpty().WithMessage("Choose an industry.");
        RuleFor(x => x.Input.CountryCode).Length(2).WithMessage("Choose a country.");
        RuleFor(x => x.Input.BaseCurrency).Length(3).WithMessage("Choose a currency.");
        RuleFor(x => x.Input.ContactName).NotEmpty().WithMessage("Enter your name.").MaximumLength(200);
        RuleFor(x => x.Input.Email).NotEmpty().WithMessage("Enter your work email.").EmailAddress().WithMessage("Enter a valid email address.").MaximumLength(256);
        RuleFor(x => x.Input.Phone).MaximumLength(40);
        RuleFor(x => x.Input.Password).NotEmpty().WithMessage("Choose a password.");
        RuleFor(x => x.Input.AcceptTerms).Equal(true).WithMessage("Please accept the terms to continue.");
    }
}
