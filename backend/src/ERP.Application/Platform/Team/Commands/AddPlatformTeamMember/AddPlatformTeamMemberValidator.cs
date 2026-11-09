using FluentValidation;

namespace ERP.Application.Platform;

public sealed class AddPlatformTeamMemberValidator : AbstractValidator<AddPlatformTeamMemberCommand>
{
    public AddPlatformTeamMemberValidator()
    {
        RuleFor(x => x.Input.Email).NotEmpty().WithMessage("Enter an email.").EmailAddress().WithMessage("Enter a valid email address.");
        RuleFor(x => x.Input.FullName).NotEmpty().WithMessage("Enter a name.").MaximumLength(200);
    }
}
