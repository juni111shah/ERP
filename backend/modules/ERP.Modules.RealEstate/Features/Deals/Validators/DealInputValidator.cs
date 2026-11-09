using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class DealInputValidator : AbstractValidator<DealInput>
{
    public DealInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.DealType).IsInEnum();
        RuleFor(x => x.ClientName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DealValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CommissionRatePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.AgentSharePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.TeamLeadSharePercent).InclusiveBetween(0, 100);
        RuleFor(x => x).Must(x => x.AgentSharePercent + x.TeamLeadSharePercent <= 100).WithName("agentSharePercent")
            .WithMessage("The agent and team-lead shares cannot exceed 100% together.");
    }
}
