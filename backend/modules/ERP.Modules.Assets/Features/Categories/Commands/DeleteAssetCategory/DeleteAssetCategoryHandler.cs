using ERP.Application.Common;
using ERP.Modules.Assets.Repositories;
using MediatR;

namespace ERP.Modules.Assets.Features;

internal sealed class DeleteAssetCategoryHandler(IAssetCategoryRepository categories, IFixedAssetRepository assets) : ICommandHandler<DeleteAssetCategoryCommand>
{
    public async Task<Unit> Handle(DeleteAssetCategoryCommand cmd, CancellationToken ct)
    {
        var category = await categories.GetForUpdateAsync(cmd.Id, ct);
        if (await assets.AnyAsync(a => a.CategoryId == category.Id, ct)) throw new ConflictException("This category has assets. Make it inactive instead.");
        categories.Remove(category);
        await categories.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
