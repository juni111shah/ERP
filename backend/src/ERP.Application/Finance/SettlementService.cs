using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

/// <summary>Allocation of customer money to invoices and the reversal of a settlement; shared by receipts and the cheque register.</summary>
public interface ISettlementService
{
    /// <summary>Validates and applies allocations to open invoices of the settlement's customer; returns the new allocation rows (the caller adds them and saves).</summary>
    Task<List<SettlementAllocation>> AllocateAsync(Settlement s, List<AllocationInput> requested, CancellationToken ct = default);
    /// <summary>Reverses the settlement's journal and takes its allocations back off the invoices. The caller sets the final status.</summary>
    Task ReverseAsync(Settlement s, string memo, CancellationToken ct = default);
}

public sealed class SettlementService(IRepository<Invoice> invoices, IRepository<JournalEntry> journals, IPostingService posting) : ISettlementService
{
    /// <summary>Validates and applies allocations to open invoices of the receipt's customer; returns the new allocation rows.</summary>
    public async Task<List<SettlementAllocation>> AllocateAsync(Settlement s, List<AllocationInput> requested, CancellationToken ct = default)
    {
        if (requested.Count == 0) return [];
        if (requested.GroupBy(r => r.InvoiceId).Any(g => g.Count() > 1)) throw new FieldValidationException("allocations", "An invoice can appear only once.");
        if (requested.Any(r => r.Amount <= 0)) throw new FieldValidationException("allocations", "Allocation amounts must be positive.");
        if (requested.Sum(r => r.Amount) > s.Unallocated) throw new FieldValidationException("allocations", "The allocations exceed the unallocated amount of the receipt.");
        var ids = requested.Select(r => r.InvoiceId).ToList();
        var open = await invoices.Tracked(new Specification<Invoice>(x => ids.Contains(x.Id))).ToListAsync(ct);
        var result = new List<SettlementAllocation>();
        foreach (var r in requested)
        {
            var inv = open.FirstOrDefault(x => x.Id == r.InvoiceId) ?? throw new FieldValidationException("allocations", "An invoice was not found.");
            if (inv.IsCreditNote || inv.PartyId != s.PartyId || inv.LegalEntityId != s.LegalEntityId) throw new FieldValidationException("allocations", $"{inv.Number} is not an invoice of this customer.");
            if (inv.Status is not (DocumentStatus.Issued or DocumentStatus.PartlyPaid)) throw new FieldValidationException("allocations", $"{inv.Number} is not open.");
            if (r.Amount > inv.Total - inv.Paid) throw new FieldValidationException("allocations", $"{inv.Number} has only {inv.Total - inv.Paid:0.00} outstanding.");
            inv.Paid += r.Amount;
            SettlementStatus.Refresh(inv);
            s.Unallocated -= r.Amount;
            result.Add(new SettlementAllocation { SettlementId = s.Id, InvoiceId = inv.Id, Amount = r.Amount });
        }
        return result;
    }

    public async Task ReverseAsync(Settlement s, string memo, CancellationToken ct = default)
    {
        var journal = await journals.Tracked(new Specification<JournalEntry>(j => j.Id == s.JournalEntryId).Include(j => j.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        await posting.ReverseAsync(journal, journal.Date, memo, ct);
        var ids = s.Allocations.Select(a => a.InvoiceId!.Value).ToList();
        foreach (var inv in await invoices.Tracked(new Specification<Invoice>(x => ids.Contains(x.Id))).ToListAsync(ct))
        {
            inv.Paid -= s.Allocations.Where(a => a.InvoiceId == inv.Id).Sum(a => a.Amount);
            SettlementStatus.Refresh(inv);
        }
        s.Unallocated = 0;
    }
}
