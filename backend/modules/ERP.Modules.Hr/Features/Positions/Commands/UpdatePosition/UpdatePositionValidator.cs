using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UpdatePositionValidator : AbstractValidator<UpdatePositionCommand> { public UpdatePositionValidator() => RuleFor(x => x.Input).SetValidator(new PositionInputValidator()); }
