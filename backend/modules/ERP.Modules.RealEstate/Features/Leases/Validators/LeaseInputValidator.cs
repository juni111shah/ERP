using ERP.Modules.RealEstate.Domain;
using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class LeaseInputValidator : AbstractValidator<LeaseInput>
{
    public LeaseInputValidator()
    {
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.LesseePartyId).NotEmpty();
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("The end date must be after the start date.");
        RuleFor(x => x.AnnualRent).GreaterThan(0).WithMessage("Annual rent must be greater than zero.");
        RuleFor(x => x.PaymentFrequency).Must(f => Lease.PaymentPlans.Contains(f)).WithMessage("Payment plan must be 1, 2, 4, 6 or 12 instalments a year.");
        RuleFor(x => x.SecurityDepositAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GraceDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.NoticePeriodDays).InclusiveBetween(0, 365);
        RuleFor(x => x).Must(x => x.StartDate.AddDays(x.GraceDays) <= (x.EndDate ?? Lease.DefaultEndDate(x.StartDate))).WithName("graceDays").WithMessage("The rent-free period must be shorter than the lease term.");
    }
}
