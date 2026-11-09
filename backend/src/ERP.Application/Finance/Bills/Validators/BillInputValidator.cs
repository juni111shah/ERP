using ERP.Application.Common;
using FluentValidation;

namespace ERP.Application.Finance;

/// <summary>Rules for a bill body; shared by create and update.</summary>
public sealed class BillInputValidator : AbstractValidator<BillInput>
{
    public BillInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.BillDate).MustBePlausibleDate();
        RuleFor(x => x.DueDate).MustBePlausibleDate();
        RuleFor(x => x).Must(x => x.DueDate is null || x.DueDate >= x.BillDate).WithName("dueDate").WithMessage("The due date cannot be before the bill date.");
        RuleFor(x => x.VendorReference).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.BusinessUnitId).NotEqual(Guid.Empty).When(x => x.BusinessUnitId is not null).WithMessage("Choose a business unit or leave it empty.");
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: > 0 }).WithMessage("A bill needs at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(400);
            l.RuleFor(x => x.Quantity).GreaterThan(0).MustHaveAtMostDecimals(4);
            l.RuleFor(x => x.Price).MustHaveAtMostDecimals(4); // negative only on a purchase variance line, checked with the accounts in BillDrafts.ApplyAsync
            l.RuleFor(x => x.AccountId).NotEmpty().WithMessage("Choose the expense account.");
        });
    }
}
