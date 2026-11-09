using FluentValidation;

namespace ERP.Application.Platform;

public sealed class CreateClientValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientValidator()
    {
        RuleFor(x => x.Input.WorkspaceName).NotEmpty().WithMessage("Enter the business name.").MaximumLength(200);
        RuleFor(x => x.Input.SignInKey).NotEmpty().WithMessage("Enter a workspace code.");
        RuleFor(x => x.Input.Industry).NotEmpty().WithMessage("Choose an industry.");
        RuleFor(x => x.Input.CountryCode).Length(2).WithMessage("Choose a country.");
        RuleFor(x => x.Input.BaseCurrency).Length(3).WithMessage("Choose a currency.");
        RuleFor(x => x.Input.AdminName).NotEmpty().WithMessage("Enter the administrator's name.").MaximumLength(200);
        RuleFor(x => x.Input.AdminEmail).NotEmpty().WithMessage("Enter the administrator's email.").EmailAddress().WithMessage("Enter a valid email address.");
        RuleFor(x => x.Input.PlanId).NotEmpty().WithMessage("Choose a plan.");
        RuleFor(x => x.Input.TrialDays).InclusiveBetween(1, 365).When(x => x.Input.TrialDays is not null).WithMessage("Use 1 to 365 days.");
        RuleFor(x => x.Input.CustomPrice).GreaterThanOrEqualTo(0).When(x => x.Input.CustomPrice is not null).WithMessage("The price cannot be negative.");
    }
}
