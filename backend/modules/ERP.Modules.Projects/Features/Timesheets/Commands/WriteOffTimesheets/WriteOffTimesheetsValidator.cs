using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class WriteOffTimesheetsValidator : AbstractValidator<WriteOffTimesheetsCommand>
{
    public WriteOffTimesheetsValidator() => RuleFor(x => x.Ids).NotEmpty().Must(i => i.Count <= 500);
}
