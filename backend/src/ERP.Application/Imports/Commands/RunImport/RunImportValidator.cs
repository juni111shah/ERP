using FluentValidation;

namespace ERP.Application.Imports;

public sealed class RunImportValidator : AbstractValidator<RunImportCommand>
{
    public RunImportValidator()
    {
        RuleFor(x => x.Key).NotEmpty();
        RuleFor(x => x.Csv).NotEmpty().WithMessage("The file is empty.").MaximumLength(5_000_000).WithMessage("The file is too large (5 MB maximum).");
    }
}
