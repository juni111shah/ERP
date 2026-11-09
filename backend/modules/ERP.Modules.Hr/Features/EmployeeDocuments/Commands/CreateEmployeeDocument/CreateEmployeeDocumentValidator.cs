using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateEmployeeDocumentValidator : AbstractValidator<CreateEmployeeDocumentCommand> { public CreateEmployeeDocumentValidator() => RuleFor(x => x.Input).SetValidator(new EmployeeDocumentInputValidator()); }
