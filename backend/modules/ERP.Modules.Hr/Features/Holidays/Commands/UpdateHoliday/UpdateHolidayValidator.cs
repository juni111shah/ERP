using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UpdateHolidayValidator : AbstractValidator<UpdateHolidayCommand> { public UpdateHolidayValidator() => RuleFor(x => x.Input).SetValidator(new HolidayInputValidator()); }
