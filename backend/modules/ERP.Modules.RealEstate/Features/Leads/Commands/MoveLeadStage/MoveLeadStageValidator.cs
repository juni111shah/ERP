using ERP.Modules.RealEstate.Domain;
using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class MoveLeadStageValidator : AbstractValidator<MoveLeadStageCommand>
{
    public MoveLeadStageValidator()
    {
        RuleFor(x => x.Stage).IsInEnum().NotEqual(LeadStage.Won).WithMessage("Convert the lead to a deal to win it.");
        RuleFor(x => x.LostReason).NotEmpty().When(x => x.Stage == LeadStage.Lost).WithMessage("Say why the lead was lost.");
        RuleFor(x => x.LostReason).MaximumLength(300);
    }
}
