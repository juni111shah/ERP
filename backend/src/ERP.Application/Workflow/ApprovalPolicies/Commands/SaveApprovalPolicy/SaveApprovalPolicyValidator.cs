using ERP.Application.Security;
using FluentValidation;

namespace ERP.Application.Workflow;

public sealed class SaveApprovalPolicyValidator : AbstractValidator<SaveApprovalPolicyCommand>
{
    public SaveApprovalPolicyValidator(IPermissionCatalog catalog)
    {
        RuleFor(x => x.Input.DocumentType).Must(t => ApprovalDocumentTypes.All.Contains(t)).WithMessage($"Approval rules are supported for: {string.Join(", ", ApprovalDocumentTypes.All)}.");
        RuleFor(x => x.Input.MinAmount).GreaterThanOrEqualTo(0).When(x => x.Input.MinAmount is not null);
        RuleFor(x => x.Input.Level).InclusiveBetween(1, 10).When(x => x.Input.Level is not null).WithMessage("The level must be between 1 and 10.");
        // A permission nobody can hold would leave every request of this policy waiting forever.
        RuleFor(x => x.Input.ApproverPermission).Must(p => catalog.All.Any(d => d.Key == p)).When(x => !string.IsNullOrWhiteSpace(x.Input.ApproverPermission)).WithMessage("Choose an existing permission for the approver.");
    }
}
