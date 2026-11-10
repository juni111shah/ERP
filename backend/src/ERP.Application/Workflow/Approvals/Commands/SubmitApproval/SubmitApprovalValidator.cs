using FluentValidation;

namespace ERP.Application.Workflow;

public sealed class SubmitApprovalValidator : AbstractValidator<SubmitApprovalCommand>
{
    public SubmitApprovalValidator() => RuleFor(x => x.Input.Subject).NotEmpty().MaximumLength(300).WithMessage("Subject is required.");
}
