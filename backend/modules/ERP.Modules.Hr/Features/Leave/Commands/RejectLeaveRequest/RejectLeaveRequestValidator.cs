using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class RejectLeaveRequestValidator : AbstractValidator<RejectLeaveRequestCommand> { public RejectLeaveRequestValidator() => RuleFor(x => x.Comment).MaximumLength(500); }
