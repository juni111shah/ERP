using ERP.Modules.Hr.Domain;
using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class StatutoryRuleInputValidator : AbstractValidator<StatutoryRuleInput>
{
    public StatutoryRuleInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Kind).IsInEnum(); RuleFor(x => x.Method).IsInEnum(); RuleFor(x => x.Base).IsInEnum();
        RuleFor(x => x.Rate).InclusiveBetween(0, 100);
        RuleFor(x => x.Threshold).GreaterThanOrEqualTo(0); RuleFor(x => x.Cap).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Nationalities).MaximumLength(300);
        RuleFor(x => x.EffectiveTo!.Value).GreaterThanOrEqualTo(x => x.EffectiveFrom!.Value).When(x => x.EffectiveFrom is not null && x.EffectiveTo is not null).WithName("EffectiveTo");
        RuleForEach(x => x.Slabs).ChildRules(s => { s.RuleFor(y => y.FromAmount).GreaterThanOrEqualTo(0); s.RuleFor(y => y.Rate).InclusiveBetween(0, 100); });
        RuleFor(x => x.Slabs).Must(s => s is { Count: > 0 }).When(x => x.Method == StatutoryMethod.Slab).WithMessage("A slab rule needs at least one slab.");
        RuleFor(x => x.Slabs).Must(s => s is null || s.Select(y => y.FromAmount).Distinct().Count() == s.Count).WithMessage("Two slabs cannot start at the same amount.");
    }
}
