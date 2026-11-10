using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UpdateEmployeeValidator : AbstractValidator<UpdateEmployeeCommand> { public UpdateEmployeeValidator() => RuleFor(x => x.Input).SetValidator(new EmployeeInputValidator()); }
