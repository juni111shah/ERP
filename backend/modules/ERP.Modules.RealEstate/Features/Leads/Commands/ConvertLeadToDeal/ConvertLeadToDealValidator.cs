using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class ConvertLeadToDealValidator : AbstractValidator<ConvertLeadToDealCommand>
{
    public ConvertLeadToDealValidator()
    {
        RuleFor(x => x.Input.DealValue).GreaterThan(0).When(x => x.Input.DealValue is not null);
        RuleFor(x => x.Input.CommissionRatePercent).InclusiveBetween(0, 100).When(x => x.Input.CommissionRatePercent is not null);
    }
}
