using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class LeaveTypeInputValidator : AbstractValidator<LeaveTypeInput>
{
    public LeaveTypeInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AnnualDays).InclusiveBetween(0, 366);
        RuleFor(x => x.CarryForwardCap).InclusiveBetween(0, 366);
        RuleFor(x => x.RequiresAttachmentAfterDays).InclusiveBetween(0, 366).When(x => x.RequiresAttachmentAfterDays is not null);
        RuleForEach(x => x.PayBands).ChildRules(b => { b.RuleFor(y => y.FromDay).InclusiveBetween(1, 366); b.RuleFor(y => y.PayPercent).InclusiveBetween(0, 100); });
        RuleFor(x => x.PayBands).Must(b => b is null || b.Select(y => y.FromDay).Distinct().Count() == b.Count).WithMessage("Two pay bands cannot start on the same day.");
        RuleForEach(x => x.Tiers).ChildRules(t => { t.RuleFor(y => y.FromServiceYears).InclusiveBetween(0, 60); t.RuleFor(y => y.Days).InclusiveBetween(0, 366); });
    }
}
