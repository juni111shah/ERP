using FluentValidation;

namespace ERP.Modules.Assets.Features;

public sealed class CreateAssetValidator : AbstractValidator<CreateAssetCommand> { public CreateAssetValidator() => RuleFor(x => x.Input).SetValidator(new AssetInputValidator()); }
