using FluentValidation;

namespace ERP.Modules.Services.Features;

public class UpdateMilestoneStatusValidator : AbstractValidator<UpdateMilestoneStatusCommand>
{
    public UpdateMilestoneStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}
