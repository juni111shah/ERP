using FluentValidation;

namespace ERP.Modules.Services.Features;

public class UpdateEngagementStatusValidator : AbstractValidator<UpdateEngagementStatusCommand>
{
    public UpdateEngagementStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}
