using ERP.Application.Abstractions;
using ERP.Application.Reminders;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Real estate reminders: leases ending within 60 days (staff, once per lease end date) and overdue rent (staff, and the tenant by email).</summary>
internal sealed class RealEstateReminders(IReadRepository<Lease> leases, IReadRepository<RentInstalment> instalments, IReadRepository<Invoice> invoices, IReadRepository<Party> parties) : IReminderSource
{
    public string? ModuleKey => RealEstateModule.Key;

    public async Task<IReadOnlyList<ReminderCandidate>> GetDueAsync(DateOnly today, CancellationToken ct)
    {
        var result = new List<ReminderCandidate>();
        var horizon = today.AddDays(60);
        var ending = await leases.Query(new Specification<Lease>(l => l.Status == LeaseStatus.Active && l.EndDate >= today && l.EndDate <= horizon)).ToListAsync(ct);
        foreach (var l in ending)
            result.Add(new($"lease-ending:{l.Id}:{l.EndDate:yyyyMMdd}", $"Lease {l.Number} ends {l.EndDate:yyyy-MM-dd}", "Renew or plan the vacate before it lapses.", $"/real-estate/leases/{l.Id}", P.LeasesEdit, Severity: "info"));

        var invoiced = await instalments.Query(new Specification<RentInstalment>(i => i.Status == InstalmentStatus.Invoiced && i.InvoiceId != null && i.DueDate < today)).ToListAsync(ct);
        var invoiceIds = invoiced.Select(i => i.InvoiceId!.Value).Distinct().ToList();
        var overdue = (await invoices.Query(new Specification<Invoice>(v => invoiceIds.Contains(v.Id) && (v.Status == DocumentStatus.Issued || v.Status == DocumentStatus.PartlyPaid))).ToListAsync(ct))
            .Where(v => v.Total - v.Paid > 0).ToDictionary(v => v.Id);
        var leaseIds = invoiced.Select(i => i.LeaseId).Distinct().ToList();
        var leaseMap = await leases.Query(new Specification<Lease>(l => leaseIds.Contains(l.Id))).ToDictionaryAsync(l => l.Id, ct);
        var partyIds = leaseMap.Values.Select(l => l.LesseePartyId).Distinct().ToList();
        var emails = await parties.Query(new Specification<Party>(p => partyIds.Contains(p.Id))).ToDictionaryAsync(p => p.Id, p => p.Email, ct);
        foreach (var i in invoiced)
        {
            if (!overdue.TryGetValue(i.InvoiceId!.Value, out var inv) || !leaseMap.TryGetValue(i.LeaseId, out var lease)) continue;
            var subject = $"Rent instalment {i.Sequence} of lease {lease.Number} is overdue";
            var body = $"{inv.Total - inv.Paid:N2} {inv.CurrencyCode} was due {i.DueDate:yyyy-MM-dd}.";
            result.Add(new($"rent-overdue:{i.Id}", subject, body, $"/real-estate/leases/{lease.Id}", P.LeasesRead, emails.GetValueOrDefault(lease.LesseePartyId)));
        }
        return result;
    }
}
