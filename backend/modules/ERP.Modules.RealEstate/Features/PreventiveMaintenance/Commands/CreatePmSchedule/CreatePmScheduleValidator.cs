using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class CreatePmScheduleValidator : AbstractValidator<CreatePmScheduleCommand> { public CreatePmScheduleValidator() => RuleFor(x => x.Input).SetValidator(new PmScheduleInputValidator()); }
