using FluentValidation;

namespace ERP.Modules.Assets.Features;

public sealed class CreateAssetCategoryValidator : AbstractValidator<CreateAssetCategoryCommand> { public CreateAssetCategoryValidator() => RuleFor(x => x.Input).SetValidator(new AssetCategoryInputValidator()); }
