using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class RequestAcceptanceValidator : AbstractValidator<RequestAcceptanceCommand>
{
    public RequestAcceptanceValidator() { RuleFor(x => x.Input.ProjectId).NotEmpty(); RuleFor(x => x.Input.Title).NotEmpty().MaximumLength(200); }
}
