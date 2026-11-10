using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateLeaveRequestValidator : AbstractValidator<CreateLeaveRequestCommand> { public CreateLeaveRequestValidator() => RuleFor(x => x.Input).SetValidator(new LeaveRequestInputValidator()); }
