using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class BulkSaveDailyAttendanceValidator : AbstractValidator<BulkSaveDailyAttendanceCommand>
{
    public BulkSaveDailyAttendanceValidator() { RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= 2000).WithMessage("Import at most 2000 rows at a time."); }
}
