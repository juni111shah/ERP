using ERP.Application.Common;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

internal sealed class UpdateAssetCategoryHandler(IAssetCategoryRepository categories, AssetCategoryRules rules) : ICommandHandler<UpdateAssetCategoryCommand, AssetCategoryDto>
{
    public async Task<AssetCategoryDto> Handle(UpdateAssetCategoryCommand cmd, CancellationToken ct)
    {
        var category = await categories.GetForUpdateAsync(cmd.Id, ct);
        await rules.ApplyAsync(category, cmd.Input, ct);
        await categories.SaveChangesAsync(ct);
        return AssetCategoryDto.From(category);
    }
}
