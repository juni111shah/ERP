using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Domain.Parties;

namespace ERP.Application.Parties;

internal sealed class ListPartiesHandler(IPartyRepository parties, ICustomFieldService fields, PartyMapping mapping) : IQueryHandler<ListPartiesQuery, PagedResult<PartyDto>>
{
    public async Task<PagedResult<PartyDto>> Handle(ListPartiesQuery query, CancellationToken ct)
    {
        // 1. The role shortcut must name a real role.
        PartyRoles? role = null;
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            if (!Enum.TryParse<PartyRoles>(query.Role, ignoreCase: true, out var parsed) || parsed == PartyRoles.None)
                throw new FieldValidationException("role", "Unknown party role.");
            role = parsed;
        }

        // 2. Page, then project each row's custom fields.
        var page = await parties.PageWithRoleAsync(query, role, ct);
        var defs = await fields.GetDefinitionsAsync(PartyMapping.EntityType, ct);
        return page.Map(p => mapping.ToDto(p, CustomFieldValidator.Project(p.CustomFields, defs)));
    }
}
