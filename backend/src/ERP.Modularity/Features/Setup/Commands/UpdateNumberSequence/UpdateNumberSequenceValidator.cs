using FluentValidation;

namespace ERP.Modularity.Features;

public sealed class UpdateNumberSequenceValidator : AbstractValidator<UpdateNumberSequenceCommand>
{
    public UpdateNumberSequenceValidator()
    {
        RuleFor(x => x.Input.Prefix).NotEmpty().WithMessage("Enter a prefix.").MaximumLength(20).Matches("^[A-Za-z0-9/_-]+$").WithMessage("Use letters, digits, dash, underscore or slash.");
        RuleFor(x => x.Input.Pattern).NotEmpty().WithMessage("Enter a pattern.").MaximumLength(60)
            .Must(p => System.Text.RegularExpressions.Regex.IsMatch(p ?? "", @"\{seq:[1-9]\d?\}")).WithMessage("The pattern must contain {seq:N}, such as {seq:6}.")
            .Must(p => System.Text.RegularExpressions.Regex.Replace(p ?? "", @"\{prefix\}|\{yyyy\}|\{yy\}|\{seq:\d+\}", "").IndexOfAny(['{', '}']) < 0).WithMessage("Use only {prefix}, {yyyy}, {yy} and {seq:N}.");
    }
}
