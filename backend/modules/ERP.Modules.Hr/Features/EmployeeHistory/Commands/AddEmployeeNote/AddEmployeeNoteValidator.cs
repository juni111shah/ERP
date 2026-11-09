using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class AddEmployeeNoteValidator : AbstractValidator<AddEmployeeNoteCommand>
{
    public AddEmployeeNoteValidator() { RuleFor(x => x.Input.Summary).NotEmpty().MaximumLength(500); }
}
