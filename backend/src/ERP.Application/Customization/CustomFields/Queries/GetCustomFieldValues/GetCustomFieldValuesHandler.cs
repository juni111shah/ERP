using System.Text.Json.Nodes;
using ERP.Application.Common;

namespace ERP.Application.Customization;

internal sealed class GetCustomFieldValuesHandler(CustomizableRecordLoader records, ICustomFieldService fields) : IQueryHandler<GetCustomFieldValuesQuery, JsonObject>
{
    public async Task<JsonObject> Handle(GetCustomFieldValuesQuery query, CancellationToken ct)
    {
        var (def, entity) = await records.LoadAsync(query.EntityType, query.RecordId, write: false, ct);
        return await fields.ReadAsync(def.Key, entity, ct);
    }
}
