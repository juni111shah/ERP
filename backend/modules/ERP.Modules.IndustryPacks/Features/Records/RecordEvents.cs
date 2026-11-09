using ERP.Application.Abstractions;
using ERP.Modules.IndustryPacks.Domain;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>The status history of a record: who moved it, when, and the move a billed record makes.</summary>
internal sealed class RecordEvents(ICurrentUser user, IClock clock)
{
    public IndustryRecordEvent NewEvent(IndustryRecord record, string? from, string to, string? reason) => new()
    {
        RecordId = record.Id, FromStatus = from, ToStatus = to, OccurredAt = clock.UtcNow, UserId = user.UserId, UserName = user.Email, Reason = reason,
    };

    public void MoveAfterBilling(IndustryRecord record, RecordTypeDef def)
    {
        if (def.Billing?.AfterBillingStatus is not { } after || after == record.Status) return;
        if (def.Billing.PerLine && record.Lines.Any(l => l.InvoiceId is null)) return;
        record.Events.Add(NewEvent(record, record.Status, after, "Billed"));
        record.Status = after;
    }
}
