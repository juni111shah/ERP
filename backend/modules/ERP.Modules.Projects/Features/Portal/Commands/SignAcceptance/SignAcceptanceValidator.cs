using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class SignAcceptanceValidator : AbstractValidator<SignAcceptanceCommand>
{
    public SignAcceptanceValidator()
    {
        RuleFor(x => x.Input.SignedByName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Input.Signature).MaximumLength(150);
        RuleFor(x => x.Input.Comment).MaximumLength(1000);
        RuleFor(x => x.Input.Comment).NotEmpty().When(x => !x.Input.Accept).WithMessage("Say why you decline.");
        RuleFor(x => x.Input).Must(i => !i.Accept || !string.IsNullOrWhiteSpace(i.Signature) || !string.IsNullOrWhiteSpace(i.SignatureImage)).WithName("signature").WithMessage("Type your name or draw your signature.");
        RuleFor(x => x.Input.SignatureImage).Must(s => s is null || (s.StartsWith("data:image/png;base64,", StringComparison.Ordinal) && s.Length <= 300_000)).WithMessage("The drawn signature must be a PNG of at most 200 KB.");
    }
}
