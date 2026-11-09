using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Distribution.Features;

public class CompleteTripValidator : AbstractValidator<CompleteTripCommand>
{
    public CompleteTripValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.TotalSalesAmount).GreaterThanOrEqualTo(0).MustBeMoney();
        RuleFor(x => x.TotalCollectionsAmount).GreaterThanOrEqualTo(0).MustBeMoney();
        RuleFor(x => x.Items).NotNull();
        RuleFor(x => x.Items).Must(i => i is null || i.Select(x => x.ItemId).Distinct().Count() == i.Count).WithMessage("An item can be reconciled only once.");
        RuleForEach(x => x.Items).ChildRules(l =>
        {
            l.RuleFor(x => x.SoldQuantity).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.ReturnedQuantity).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.DamagedQuantity).GreaterThanOrEqualTo(0);
        });
    }
}
