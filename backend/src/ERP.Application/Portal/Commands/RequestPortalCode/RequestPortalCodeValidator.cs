using FluentValidation;

namespace ERP.Application.Portal;

public sealed class RequestPortalCodeValidator : AbstractValidator<RequestPortalCodeCommand>
{
    public RequestPortalCodeValidator() { RuleFor(x => x.Workspace).NotEmpty(); RuleFor(x => x.Email).NotEmpty().EmailAddress(); RuleFor(x => x.Kind).Must(k => k is null or "customer" or "vendor").WithMessage("Kind must be customer or vendor."); }
}
