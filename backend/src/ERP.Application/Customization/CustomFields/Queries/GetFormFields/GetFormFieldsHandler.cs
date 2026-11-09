using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Customization;

internal sealed class GetFormFieldsHandler(ICustomFieldDefinitionRepository definitions, ICustomizableEntityRegistry registry, ICurrentUser user)
    : IQueryHandler<GetFormFieldsQuery, IReadOnlyList<CustomFieldDefinitionDto>>
{
    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> Handle(GetFormFieldsQuery query, CancellationToken ct)
    {
        // 1. The entity type must accept custom fields and the caller must be able to read it.
        var entity = registry.Find(query.EntityType) ?? throw new NotFoundException();
        if (entity.Permission is { } permission && !user.HasPermission(permission)) throw new ForbiddenException();

        // 2. Its active definitions, in form order.
        return (await definitions.ListActiveForEntityTypeAsync(query.EntityType, ct)).Select(d => d.ToDto()).ToList();
    }
}
