using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class TerminateEmployeeValidator : AbstractValidator<TerminateEmployeeCommand> { public TerminateEmployeeValidator() => RuleFor(x => x.Input).SetValidator(new TerminateInputValidator()); }
