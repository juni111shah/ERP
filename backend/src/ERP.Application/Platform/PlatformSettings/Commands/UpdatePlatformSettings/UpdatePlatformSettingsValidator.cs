using FluentValidation;

namespace ERP.Application.Platform;

public sealed class UpdatePlatformSettingsValidator : AbstractValidator<UpdatePlatformSettingsCommand>
{
    public UpdatePlatformSettingsValidator()
    {
        RuleFor(x => x.Input.Currency).Length(3).WithMessage("Use a 3-letter currency code.");
        RuleFor(x => x.Input.TaxPercent).InclusiveBetween(0, 100).WithMessage("Use 0 to 100.");
        RuleFor(x => x.Input.TaxLabel).NotEmpty().WithMessage("Enter a tax label, such as VAT.").MaximumLength(40);
        RuleFor(x => x.Input.InvoicePrefix).NotEmpty().WithMessage("Enter an invoice prefix.").Matches("^[A-Za-z0-9]{1,10}$").WithMessage("Use up to 10 letters or digits.");
        RuleFor(x => x.Input.InvoiceDueDays).InclusiveBetween(0, 120).WithMessage("Use 0 to 120 days.");
        RuleFor(x => x.Input.RenewalLeadDays).InclusiveBetween(0, 60).WithMessage("Use 0 to 60 days.");
        RuleFor(x => x.Input.GraceDays).InclusiveBetween(0, 120).WithMessage("Use 0 to 120 days.");
        RuleFor(x => x.Input.SuspendAfterDays).InclusiveBetween(1, 365).WithMessage("Use 1 to 365 days.");
        RuleFor(x => x.Input).Must(i => i.SuspendAfterDays > i.GraceDays).WithName("suspendAfterDays").WithMessage("Suspension must come after the grace period.");
        RuleFor(x => x.Input.PaymentInstructions).MaximumLength(2000);
        RuleFor(x => x.Input.AboutText).MaximumLength(2000);
        RuleFor(x => x.Input.TermsText).MaximumLength(100_000);
        RuleFor(x => x.Input.PrivacyText).MaximumLength(100_000);
        RuleFor(x => x.Input.TermsUrl).Must(u => Uri.TryCreate(u, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https").When(x => !string.IsNullOrWhiteSpace(x.Input.TermsUrl)).WithMessage("Enter a full web address, starting with https://.");
        RuleFor(x => x.Input.SupportEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Input.SupportEmail)).WithMessage("Enter a valid email address.");
    }
}
