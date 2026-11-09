using ERP.Application.Common;
using ERP.Modules.Assets.Domain;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

internal sealed class CreateAssetCategoryHandler(IAssetCategoryRepository categories, AssetCategoryRules rules) : ICommandHandler<CreateAssetCategoryCommand, AssetCategoryDto>
{
    public async Task<AssetCategoryDto> Handle(CreateAssetCategoryCommand cmd, CancellationToken ct)
    {
        var category = new AssetCategory();
        await rules.ApplyAsync(category, cmd.Input, ct);
        categories.Add(category);
        await categories.SaveChangesAsync(ct);
        return AssetCategoryDto.From(category);
    }
}
