using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UpdateLeaveTypeValidator : AbstractValidator<UpdateLeaveTypeCommand> { public UpdateLeaveTypeValidator() => RuleFor(x => x.Input).SetValidator(new LeaveTypeInputValidator()); }
