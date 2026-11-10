using ERP.Application.Common;
using FluentValidation;

namespace ERP.Application.Finance;

/// <summary>Rules for an invoice body; shared by create and update.</summary>
public sealed class InvoiceInputValidator : AbstractValidator<InvoiceInput>
{
    public InvoiceInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.InvoiceDate).MustBePlausibleDate();
        RuleFor(x => x.DueDate).MustBePlausibleDate();
        RuleFor(x => x).Must(x => x.DueDate is null || x.DueDate >= x.InvoiceDate).WithName("dueDate").WithMessage("The due date cannot be before the invoice date.");
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: > 0 }).WithMessage("An invoice needs at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(400);
            l.RuleFor(x => x.Quantity).GreaterThan(0).MustHaveAtMostDecimals(4);
            l.RuleFor(x => x.Price).GreaterThanOrEqualTo(0).MustHaveAtMostDecimals(4);
        });
    }
}
