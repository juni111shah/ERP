using ERP.Application.Common;

namespace ERP.Application.Customization;

internal sealed class ListCustomizableEntitiesHandler(ICustomizableEntityRegistry registry) : IQueryHandler<ListCustomizableEntitiesQuery, IReadOnlyList<CustomizableEntityDto>>
{
    public Task<IReadOnlyList<CustomizableEntityDto>> Handle(ListCustomizableEntitiesQuery query, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CustomizableEntityDto>>(registry.All.Select(d => new CustomizableEntityDto(d.Key, d.Label, d.ModuleKey)).ToList());
}
