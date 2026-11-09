using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CandidateInputValidator : AbstractValidator<CandidateInput>
{
    public CandidateInputValidator()
    {
        RuleFor(x => x.OpeningId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50); RuleFor(x => x.Source).MaximumLength(100); RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.ExpectedSalary!.Value).GreaterThanOrEqualTo(0).When(x => x.ExpectedSalary is not null).WithName("ExpectedSalary");
    }
}
