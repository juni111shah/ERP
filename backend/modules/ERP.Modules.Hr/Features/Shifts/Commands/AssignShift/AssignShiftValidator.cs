using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class AssignShiftValidator : AbstractValidator<AssignShiftCommand> { public AssignShiftValidator() => RuleFor(x => x.EmployeeIds).NotEmpty().Must(i => i.Count <= 500); }
