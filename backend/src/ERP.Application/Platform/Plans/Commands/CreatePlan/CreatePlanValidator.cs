using FluentValidation;

namespace ERP.Application.Platform;

public sealed class CreatePlanValidator : AbstractValidator<CreatePlanCommand> { public CreatePlanValidator() => RuleFor(x => x.Input).SetValidator(new PlanInputValidator()); }
