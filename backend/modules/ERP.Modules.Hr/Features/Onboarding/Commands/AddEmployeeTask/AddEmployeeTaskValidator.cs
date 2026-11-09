using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class AddEmployeeTaskValidator : AbstractValidator<AddEmployeeTaskCommand>
{
    public AddEmployeeTaskValidator() { RuleFor(x => x.Input.Title).NotEmpty().MaximumLength(200); RuleFor(x => x.Input.Notes).MaximumLength(500); }
}
