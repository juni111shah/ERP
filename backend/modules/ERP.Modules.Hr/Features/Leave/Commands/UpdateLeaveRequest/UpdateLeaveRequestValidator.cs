using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UpdateLeaveRequestValidator : AbstractValidator<UpdateLeaveRequestCommand> { public UpdateLeaveRequestValidator() => RuleFor(x => x.Input).SetValidator(new LeaveRequestInputValidator()); }
