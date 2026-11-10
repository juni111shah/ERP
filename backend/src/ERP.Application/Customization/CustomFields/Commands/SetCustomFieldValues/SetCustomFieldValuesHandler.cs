using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Customization;

internal sealed class SetCustomFieldValuesHandler(CustomizableRecordLoader records, ICustomFieldService fields, IEntityStore entities) : ICommandHandler<SetCustomFieldValuesCommand, JsonObject>
{
    public async Task<JsonObject> Handle(SetCustomFieldValuesCommand command, CancellationToken ct)
    {
        var (def, entity) = await records.LoadAsync(command.EntityType, command.RecordId, write: true, ct);
        await fields.ApplyAsync(def.Key, entity, command.Values, isCreate: false, ct);
        await entities.SaveChangesAsync(ct);
        return await fields.ReadAsync(def.Key, entity, ct);
    }
}
