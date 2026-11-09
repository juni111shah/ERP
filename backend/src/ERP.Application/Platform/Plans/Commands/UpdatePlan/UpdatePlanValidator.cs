using FluentValidation;

namespace ERP.Application.Platform;

public sealed class UpdatePlanValidator : AbstractValidator<UpdatePlanCommand> { public UpdatePlanValidator() => RuleFor(x => x.Input).SetValidator(new PlanInputValidator()); }
