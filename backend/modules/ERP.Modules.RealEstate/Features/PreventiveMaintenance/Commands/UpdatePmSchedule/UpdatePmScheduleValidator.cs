using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class UpdatePmScheduleValidator : AbstractValidator<UpdatePmScheduleCommand> { public UpdatePmScheduleValidator() => RuleFor(x => x.Input).SetValidator(new PmScheduleInputValidator()); }
