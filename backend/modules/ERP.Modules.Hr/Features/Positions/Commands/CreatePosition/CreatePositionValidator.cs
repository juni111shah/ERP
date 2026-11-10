using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreatePositionValidator : AbstractValidator<CreatePositionCommand> { public CreatePositionValidator() => RuleFor(x => x.Input).SetValidator(new PositionInputValidator()); }
