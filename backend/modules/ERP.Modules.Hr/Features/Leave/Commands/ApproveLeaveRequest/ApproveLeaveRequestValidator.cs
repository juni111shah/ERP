using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class ApproveLeaveRequestValidator : AbstractValidator<ApproveLeaveRequestCommand> { public ApproveLeaveRequestValidator() => RuleFor(x => x.Comment).MaximumLength(500); }
