using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UpdateDepartmentValidator : AbstractValidator<UpdateDepartmentCommand> { public UpdateDepartmentValidator() => RuleFor(x => x.Input).SetValidator(new DepartmentInputValidator()); }
