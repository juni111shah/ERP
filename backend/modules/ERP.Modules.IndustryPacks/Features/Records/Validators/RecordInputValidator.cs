using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.IndustryPacks.Features;

public sealed class RecordInputValidator : AbstractValidator<RecordInput>
{
    public RecordInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty().WithMessage("Choose a legal entity.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Enter a name.").MaximumLength(300);
        RuleFor(x => x.RecordDate).MustBePlausibleDate();
        RuleFor(x => x.DueDate).Must(d => d is null || d >= new DateOnly(2000, 1, 1)).WithMessage("Enter a valid date.");
        RuleFor(x => x.Notes).MaximumLength(4000);
        RuleFor(x => x.Amount).Must(a => a is null || a >= 0).WithMessage("The amount cannot be negative.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty().WithMessage("Enter a description.").MaximumLength(500);
            l.RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).WithMessage("The quantity cannot be negative.");
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("The price cannot be negative.");
            l.RuleFor(x => x.CompletedQuantity).Must(q => q is null || q >= 0).WithMessage("The completed quantity cannot be negative.");
        });
    }
}
