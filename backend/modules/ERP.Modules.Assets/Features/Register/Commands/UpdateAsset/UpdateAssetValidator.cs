using FluentValidation;

namespace ERP.Modules.Assets.Features;

public sealed class UpdateAssetValidator : AbstractValidator<UpdateAssetCommand> { public UpdateAssetValidator() => RuleFor(x => x.Input).SetValidator(new AssetInputValidator()); }
