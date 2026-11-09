using ERP.Application.Common;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

internal sealed class GetAssetHandler(IFixedAssetRepository assets) : IQueryHandler<GetAssetQuery, AssetDto>
{
    public async Task<AssetDto> Handle(GetAssetQuery q, CancellationToken ct) => AssetDto.From(await assets.GetAsync(q.Id, ct));
}
