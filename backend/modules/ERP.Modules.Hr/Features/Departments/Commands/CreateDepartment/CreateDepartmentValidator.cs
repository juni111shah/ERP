using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateDepartmentValidator : AbstractValidator<CreateDepartmentCommand> { public CreateDepartmentValidator() => RuleFor(x => x.Input).SetValidator(new DepartmentInputValidator()); }
