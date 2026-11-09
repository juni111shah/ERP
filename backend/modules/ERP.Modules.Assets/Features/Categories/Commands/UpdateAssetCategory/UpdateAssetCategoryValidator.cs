using FluentValidation;

namespace ERP.Modules.Assets.Features;

public sealed class UpdateAssetCategoryValidator : AbstractValidator<UpdateAssetCategoryCommand> { public UpdateAssetCategoryValidator() => RuleFor(x => x.Input).SetValidator(new AssetCategoryInputValidator()); }
