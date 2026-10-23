using ERP.Application.Security;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Abstractions;

namespace ERP.Application.Reminders;

/// <summary>Core reminders: vendor bills falling due within three days or overdue, and post-dated cheques maturing within three days.</summary>
public sealed class CoreReminderSource(IReadRepository<Bill> bills, IReadRepository<PaymentInstrument> cheques, IReadRepository<ERP.Domain.Platform.Attachment> documents) : IReminderSource
{
    public string? ModuleKey => null;

    public async Task<IReadOnlyList<ReminderCandidate>> GetDueAsync(DateOnly today, CancellationToken ct)
    {
        var horizon = today.AddDays(3);
        var result = new List<ReminderCandidate>();
        var openBills = await bills.Query(new Specification<Bill>(b => (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid) && b.DueDate <= horizon)).ToListAsync(ct);
        foreach (var b in openBills.Where(b => b.Total - b.Paid > 0))
        {
            var overdue = b.DueDate < today;
            result.Add(new($"bill-due:{b.Id}:{(overdue ? "overdue" : "soon")}", overdue ? $"Bill {b.Number} is overdue" : $"Bill {b.Number} is due {b.DueDate:yyyy-MM-dd}",
                $"{b.PartyName}: {b.Total - b.Paid:N2} {b.CurrencyCode} outstanding", "/finance/bills", CorePermissions.BillsWrite));
        }
        var maturing = await cheques.Query(new Specification<PaymentInstrument>(c => c.Status == InstrumentStatus.InCustody && c.InstrumentDate <= horizon)).ToListAsync(ct);
        foreach (var c in maturing)
            result.Add(new($"cheque-due:{c.Id}", $"Cheque {c.InstrumentNo} matures {c.InstrumentDate:yyyy-MM-dd}", $"{c.PartyName}: {c.Amount:N2} {c.CurrencyCode}", "/finance/cheques", CorePermissions.ChequesApprove, Severity: "info"));
        var expiring = await documents.Query(new Specification<ERP.Domain.Platform.Attachment>(a => a.ExpiresOn != null && a.ExpiresOn <= today.AddDays(30))).ToListAsync(ct);
        foreach (var d in expiring)
        {
            var gone = d.ExpiresOn!.Value < today;
            result.Add(new($"doc-expiry:{d.Id}:{(gone ? "expired" : "soon")}", gone ? $"{d.Category ?? "Document"} expired: {d.FileName}" : $"{d.Category ?? "Document"} expires {d.ExpiresOn:yyyy-MM-dd}: {d.FileName}",
                d.Description, "/admin/documents", CorePermissions.AttachmentsWrite, Severity: gone ? "error" : "warning"));
        }
        return result;
    }
}
