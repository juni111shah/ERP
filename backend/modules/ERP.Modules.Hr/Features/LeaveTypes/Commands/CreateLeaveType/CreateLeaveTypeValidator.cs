using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateLeaveTypeValidator : AbstractValidator<CreateLeaveTypeCommand> { public CreateLeaveTypeValidator() => RuleFor(x => x.Input).SetValidator(new LeaveTypeInputValidator()); }
