using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Modules.IndustryPacks.Domain;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>Shapes a record for the caller: restricted fields only with the sensitive permission, and only the next steps they may take.</summary>
internal sealed class RecordDtoBuilder(ICurrentUser user)
{
    public RecordDto ToDto(IndustryRecord r, RecordTypeDef def, IReadOnlyList<IndustryRecordLine> lines)
    {
        var data = JsonNode.Parse(string.IsNullOrWhiteSpace(r.Data) ? "{}" : r.Data)?.AsObject() ?? [];
        if (!user.HasPermission(PackModule.SensitivePermission(r.ModuleKey, def.Key)))
            foreach (var field in def.Fields.Where(f => f.Sensitive)) data.Remove(field.Key);
        var next = def.Transitions.Where(t => t.From == r.Status && (!t.Approval || user.HasPermission(PackModule.ApprovePermission(r.ModuleKey, def.Key)))).ToList();
        return new RecordDto(r.Id, r.ModuleKey, r.RecordType, r.LegalEntityId, r.Number, r.Title, r.Status, r.PartyId, r.PartyName, r.ParentId, r.ParentTitle, r.BusinessUnitId, r.RecordDate, r.DueDate, r.Amount, data,
            r.Notes, r.InvoiceId, r.BillId, r.ConsumedOn, r.CreatedAt,
            lines.OrderBy(l => l.Seq).Select(l => new RecordLineDto(l.Id, l.Seq, l.Code, l.Description, l.ItemId, l.UnitOfMeasure, l.Quantity, l.UnitPrice, l.Amount, l.CompletedQuantity, l.DueDate, l.InvoiceId)).ToList(),
            r.Events.OrderBy(e => e.OccurredAt).Select(e => new RecordEventDto(e.Id, e.FromStatus, e.ToStatus, e.OccurredAt, e.UserName, e.Reason)).ToList(), next);
    }
}
