using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UpdateShiftValidator : AbstractValidator<UpdateShiftCommand> { public UpdateShiftValidator() => RuleFor(x => x.Input).SetValidator(new ShiftInputValidator()); }
