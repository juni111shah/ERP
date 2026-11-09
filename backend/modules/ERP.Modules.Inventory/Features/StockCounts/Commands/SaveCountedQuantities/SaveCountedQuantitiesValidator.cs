using FluentValidation;

namespace ERP.Modules.Inventory.Features;

public sealed class SaveCountedQuantitiesValidator : AbstractValidator<SaveCountedQuantitiesCommand>
{
    public SaveCountedQuantitiesValidator()
    {
        RuleFor(x => x.Lines).NotNull().Must(l => l is null || l.Count <= 2000).WithMessage("At most 2000 lines.");
        RuleForEach(x => x.Lines).ChildRules(l => { l.RuleFor(x => x.ItemId).NotEmpty(); l.RuleFor(x => x.CountedQuantity).GreaterThanOrEqualTo(0).When(x => x.CountedQuantity is not null); });
    }
}
