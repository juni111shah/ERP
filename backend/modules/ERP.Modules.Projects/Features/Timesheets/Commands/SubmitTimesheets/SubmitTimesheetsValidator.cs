using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class SubmitTimesheetsValidator : AbstractValidator<SubmitTimesheetsCommand>
{
    public SubmitTimesheetsValidator() => RuleFor(x => x.Ids).NotEmpty().Must(i => i.Count <= 500);
}
