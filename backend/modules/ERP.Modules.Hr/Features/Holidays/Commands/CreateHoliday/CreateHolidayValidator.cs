using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateHolidayValidator : AbstractValidator<CreateHolidayCommand> { public CreateHolidayValidator() => RuleFor(x => x.Input).SetValidator(new HolidayInputValidator()); }
