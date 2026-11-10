using ERP.Application.Common;
using ERP.Modules.IndustryPacks.Domain;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>Rules of a record that need nothing but the record and its definition.</summary>
internal static class RecordRules
{
    public static void EnsureBillable(IndustryRecord record, RecordTypeDef def)
    {
        if (def.Billing?.WhenStatus is { Count: > 0 } allowed && !allowed.Contains(record.Status))
            throw new ConflictException($"A record in status '{record.Status}' cannot be billed yet.");
    }

    public static void AddLines(IndustryRecord record, RecordTypeDef def, List<RecordLineInput>? lines)
    {
        if (lines is null) return;
        if (!def.HasLines && lines.Count > 0) throw new FieldValidationException("lines", $"{def.Label} do not have lines.");
        var seq = 0;
        foreach (var l in lines)
        {
            record.Lines.Add(new IndustryRecordLine
            {
                RecordId = record.Id, Seq = ++seq, Code = l.Code?.Trim(), Description = l.Description.Trim(), ItemId = l.ItemId, UnitOfMeasure = l.UnitOfMeasure?.Trim(),
                Quantity = l.Quantity, UnitPrice = l.UnitPrice, Amount = Math.Round(l.Quantity * l.UnitPrice, 2, MidpointRounding.AwayFromZero),
                CompletedQuantity = l.CompletedQuantity ?? 0, DueDate = l.DueDate,
            });
        }
    }

    public static void Recalculate(IndustryRecord record, RecordTypeDef def, decimal? amount)
    {
        if (def.HasLines && record.Lines.Count > 0) record.Amount = record.Lines.Sum(l => l.Amount);
        else if (def.HasAmount) record.Amount = Math.Round(amount ?? 0, 2, MidpointRounding.AwayFromZero);
        else record.Amount = 0;
    }
}
