using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UpdateEmployeeDocumentValidator : AbstractValidator<UpdateEmployeeDocumentCommand> { public UpdateEmployeeDocumentValidator() => RuleFor(x => x.Input).SetValidator(new EmployeeDocumentInputValidator()); }
