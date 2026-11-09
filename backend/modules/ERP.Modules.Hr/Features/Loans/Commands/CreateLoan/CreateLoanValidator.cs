using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateLoanValidator : AbstractValidator<CreateLoanCommand> { public CreateLoanValidator() => RuleFor(x => x.Input).SetValidator(new LoanInputValidator()); }
