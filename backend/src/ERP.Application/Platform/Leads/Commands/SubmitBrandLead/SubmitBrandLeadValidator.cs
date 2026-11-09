using FluentValidation;

namespace ERP.Application.Platform;

public sealed class SubmitBrandLeadValidator : AbstractValidator<SubmitBrandLeadCommand>
{
    public SubmitBrandLeadValidator()
    {
        RuleFor(x => x.Input.Name).NotEmpty().WithMessage("Enter your name.").MaximumLength(120);
        RuleFor(x => x.Input.Email).NotEmpty().WithMessage("Enter your email.").EmailAddress().WithMessage("Enter a valid email address.").MaximumLength(256);
        RuleFor(x => x.Input.Phone).MaximumLength(40);
        RuleFor(x => x.Input.Company).MaximumLength(200);
        RuleFor(x => x.Input.Need).MaximumLength(1000);
        RuleFor(x => x.Input.Consent).Equal(true).WithMessage("Please agree so that we can contact you.");
    }
}
