using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class EmployeeDocumentInputValidator : AbstractValidator<EmployeeDocumentInput>
{
    public EmployeeDocumentInputValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Number).MaximumLength(100);
        RuleFor(x => x.IssuedBy).MaximumLength(150);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x).Must(x => x.IssueDate is null || x.ExpiryDate is null || x.ExpiryDate >= x.IssueDate).WithName("expiryDate").WithMessage("The expiry date cannot be before the issue date.");
    }
}
