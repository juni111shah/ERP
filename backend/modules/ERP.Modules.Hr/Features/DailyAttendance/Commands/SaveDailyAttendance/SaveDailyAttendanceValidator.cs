using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class SaveDailyAttendanceValidator : AbstractValidator<SaveDailyAttendanceCommand> { public SaveDailyAttendanceValidator() => RuleFor(x => x.Input).SetValidator(new DailyAttendanceInputValidator()); }
