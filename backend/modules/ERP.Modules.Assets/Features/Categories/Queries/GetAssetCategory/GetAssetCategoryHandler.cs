using ERP.Application.Common;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

internal sealed class GetAssetCategoryHandler(IAssetCategoryRepository categories) : IQueryHandler<GetAssetCategoryQuery, AssetCategoryDto>
{
    public async Task<AssetCategoryDto> Handle(GetAssetCategoryQuery q, CancellationToken ct) => AssetCategoryDto.From(await categories.GetAsync(q.Id, ct));
}
