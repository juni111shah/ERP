using ERP.Application.Common;

namespace ERP.Application.Customization;

internal sealed class ListCustomFieldDefinitionsHandler(ICustomFieldDefinitionRepository definitions) : IQueryHandler<ListCustomFieldDefinitionsQuery, IReadOnlyList<CustomFieldDefinitionDto>>
{
    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> Handle(ListCustomFieldDefinitionsQuery query, CancellationToken ct)
        => (await definitions.ListForEntityTypeAsync(query.EntityType, query.IncludeInactive, ct)).Select(d => d.ToDto()).ToList();
}
