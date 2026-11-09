using FluentValidation;

namespace ERP.Application.Platform;

public sealed class RequestPlanChangeValidator : AbstractValidator<RequestPlanChangeCommand>
{
    public RequestPlanChangeValidator()
    {
        RuleFor(x => x.Input.PlanId).NotEmpty().WithMessage("Choose a plan.");
        RuleFor(x => x.Input.Note).MaximumLength(400);
    }
}
