using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateShiftValidator : AbstractValidator<CreateShiftCommand> { public CreateShiftValidator() => RuleFor(x => x.Input).SetValidator(new ShiftInputValidator()); }
