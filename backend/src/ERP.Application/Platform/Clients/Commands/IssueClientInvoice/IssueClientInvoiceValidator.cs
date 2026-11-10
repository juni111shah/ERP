using FluentValidation;

namespace ERP.Application.Platform;

public sealed class IssueClientInvoiceValidator : AbstractValidator<IssueClientInvoiceCommand>
{
    public IssueClientInvoiceValidator()
    {
        RuleFor(x => x.Input.Amount).GreaterThan(0).When(x => x.Input.Amount is not null).WithMessage("Enter an amount above zero.");
        RuleFor(x => x.Input.Description).NotEmpty().When(x => x.Input.Amount is not null).WithMessage("Describe what this charge is for.");
    }
}
