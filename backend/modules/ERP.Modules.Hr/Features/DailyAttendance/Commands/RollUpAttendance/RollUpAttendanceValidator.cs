using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class RollUpAttendanceValidator : AbstractValidator<RollUpAttendanceCommand>
{
    public RollUpAttendanceValidator() { RuleFor(x => x.Year).InclusiveBetween(2000, 2100); RuleFor(x => x.Month).InclusiveBetween(1, 12); }
}
