using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

/// <summary>Allocation of customer money to invoices and the reversal of a settlement; shared by receipts and the cheque register.</summary>
public interface ISettlementService
{
    /// <summary>Validates and applies allocations to open invoices of the settlement's customer; returns the new allocation rows (the caller adds them and saves).</summary>
    Task<List<SettlementAllocation>> AllocateAsync(Settlement s, List<AllocationInput> requested, CancellationToken ct = default);
    /// <summary>Reverses the settlement's journal and takes its allocations back off the invoices. The caller sets the final status.</summary>
    Task ReverseAsync(Settlement s, string memo, CancellationToken ct = default);
    /// <summary>Oldest-due-first allocation of what is still unallocated to the customer's open invoices.</summary>
    Task<List<SettlementAllocation>> AutoAllocateAsync(Settlement s, CancellationToken ct = default);
}

/// <summary>Raised when an invoice that came from a module document (SourceType / SourceId) becomes fully paid or is reopened.</summary>
public sealed record InvoicePaymentChanged(Guid InvoiceId, string? SourceType, Guid? SourceId, bool IsPaid) : INotification;

public sealed class SettlementService(IRepository<Invoice> invoices, IRepository<JournalEntry> journals, IPostingService posting, IPublisher publisher) : ISettlementService
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
        foreach (var inv in open.Where(o => o.SourceType != null && o.Status == DocumentStatus.Paid)) await publisher.Publish(new InvoicePaymentChanged(inv.Id, inv.SourceType, inv.SourceId, true), ct);
        return result;
    }

    public async Task ReverseAsync(Settlement s, string memo, CancellationToken ct = default)
    {
        var journal = await journals.Tracked(new Specification<JournalEntry>(j => j.Id == s.JournalEntryId).Include(j => j.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        await posting.ReverseTodayAsync(journal, memo, ct);
        var ids = s.Allocations.Select(a => a.InvoiceId!.Value).ToList();
        foreach (var inv in await invoices.Tracked(new Specification<Invoice>(x => ids.Contains(x.Id))).ToListAsync(ct))
        {
            var wasPaid = inv.Status == DocumentStatus.Paid;
            inv.Paid -= s.Allocations.Where(a => a.InvoiceId == inv.Id).Sum(a => a.Amount);
            SettlementStatus.Refresh(inv);
            if (wasPaid && inv.SourceType != null) await publisher.Publish(new InvoicePaymentChanged(inv.Id, inv.SourceType, inv.SourceId, false), ct);
        }
        s.Unallocated = 0;
    }

    public async Task<List<SettlementAllocation>> AutoAllocateAsync(Settlement s, CancellationToken ct = default)
    {
        if (s.Unallocated <= 0) return [];
        var open = await invoices.Query(new Specification<Invoice>(i => i.PartyId == s.PartyId && i.LegalEntityId == s.LegalEntityId && !i.IsCreditNote && (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid)))
            .OrderBy(i => i.DueDate).ThenBy(i => i.InvoiceDate).Select(i => new { i.Id, i.Total, i.Paid }).ToListAsync(ct);
        var left = s.Unallocated; var requested = new List<AllocationInput>();
        foreach (var i in open)
        {
            if (left <= 0) break;
            var take = Math.Min(left, i.Total - i.Paid);
            if (take > 0) { requested.Add(new AllocationInput(i.Id, take)); left -= take; }
        }
        return await AllocateAsync(s, requested, ct);
    }
}
