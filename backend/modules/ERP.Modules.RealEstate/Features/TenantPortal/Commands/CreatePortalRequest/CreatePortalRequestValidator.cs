using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class CreatePortalRequestValidator : AbstractValidator<CreatePortalRequestCommand>
{
    public CreatePortalRequestValidator()
    {
        RuleFor(x => x.Input.LeaseId).NotEmpty();
        RuleFor(x => x.Input.Issue).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Input.Priority).IsInEnum();
        RuleFor(x => x.Input.Category).MaximumLength(50);
    }
}
