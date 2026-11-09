using FluentValidation;

namespace ERP.Modules.Services.Features;

public sealed class InvoiceEngagementTimeValidator : AbstractValidator<InvoiceEngagementTimeCommand>
{
    public InvoiceEngagementTimeValidator()
    {
        RuleFor(x => x.EngagementId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To >= x.From).WithName("to").WithMessage("The end date cannot be before the start date.");
    }
}
