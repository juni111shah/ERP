using FluentValidation;

namespace ERP.Modules.Services.Features;

public class CreateEngagementValidator : AbstractValidator<CreateEngagementCommand>
{
    public CreateEngagementValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty().WithMessage("Legal entity is required.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).WithMessage("Title is required.");
        RuleFor(x => x.ClientId).NotEmpty().WithMessage("Client is required.");
        RuleFor(x => x.ClientName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContractValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BudgetedHours).GreaterThanOrEqualTo(0);
    }
}
