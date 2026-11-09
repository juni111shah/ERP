using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class HolidayInputValidator : AbstractValidator<HolidayInput> { public HolidayInputValidator() { RuleFor(x => x.Name).NotEmpty().MaximumLength(150); } }
