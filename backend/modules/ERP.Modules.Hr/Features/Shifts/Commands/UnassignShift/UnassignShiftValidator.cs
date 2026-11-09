using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UnassignShiftValidator : AbstractValidator<UnassignShiftCommand> { public UnassignShiftValidator() => RuleFor(x => x.EmployeeIds).NotEmpty().Must(i => i.Count <= 500); }
