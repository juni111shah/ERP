using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Parties;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

/// <summary>
/// Rules shared by create, update and delete bill: pricing a draft from its input (vendor, accounts, tax codes, items, business unit),
/// saving it with the duplicate vendor reference check, and withdrawing a pending approval request.
/// </summary>
public sealed class BillDrafts(
    IBillRepository bills,
    IReadRepository<Party> parties,
    IAccountRepository accounts,
    ITaxCodeRepository taxCodes,
    IReadRepository<BusinessUnit> businessUnits,
    IReferenceCheck references,
    IDbErrors dbErrors,
    IReadRepository<ApprovalRequest> approvalRequests,
    IApprovalService approvals)
{
    public const string VendorReferenceIndex = "IX_Bills_OrgId_PartyId_VendorReference";

    /// <summary>Validates the input against the vendor, accounts, tax codes, items and business unit, sets the bill's header and totals and returns its new lines.</summary>
    public async Task<List<BillLine>> ApplyAsync(Bill bill, BillInput i, CancellationToken ct)
    {
        var party = await parties.FindAsync(i.PartyId, ct) ?? throw new FieldValidationException("partyId", "Vendor not found.");
        VendorRules.RequireVendor(party);
        var reference = i.VendorReference?.Trim();
        if (!string.IsNullOrEmpty(reference) && await bills.AnyAsync(b => b.PartyId == party.Id && b.VendorReference == reference && b.Id != bill.Id && b.Status != DocumentStatus.Voided, ct))
            throw new ConflictException(DuplicateReferenceMessage(reference));

        var accountIds = i.Lines.Select(l => l.AccountId).Distinct().ToList();
        var found = await accounts.ListPostableRolesAsync(accountIds, ct);
        if (found.Count != accountIds.Count) throw new FieldValidationException("lines", "An account was not found or cannot be posted to.");
        if (i.Lines.Any(l => l.Price < 0 && found.First(a => a.Id == l.AccountId).SystemRole != SystemRoles.PurchaseVariance))
            throw new FieldValidationException("lines", "A price cannot be negative, except on a purchase price variance line.");
        var taxIds = i.Lines.Where(l => l.TaxCodeId is not null).Select(l => l.TaxCodeId!.Value).Distinct().ToList();
        var taxes = await taxCodes.ActiveRatesAsync(taxIds, ct);
        if (taxes.Count != taxIds.Count) throw new FieldValidationException("lines", "A tax code was not found or is no longer active.");
        var itemIds = i.Lines.Where(l => l.ItemId is not null).Select(l => l.ItemId!.Value).Distinct().ToList();
        if (!await references.AllExistAsync<Item>(itemIds, ct))
            throw new FieldValidationException("lines", "An item was not found.");
        if (i.BusinessUnitId is { } unitId && unitId != bill.BusinessUnitId && !await businessUnits.AnyAsync(u => u.Id == unitId && u.LegalEntityId == bill.LegalEntityId, ct))
            throw new FieldValidationException("businessUnitId", "Business unit not found in this legal entity.");

        var seq = 1;
        var result = i.Lines.Select(l =>
        {
            var net = Money.Round(l.Quantity * l.Price);
            var tax = l.TaxCodeId is { } t ? Money.Round(net * taxes[t].Rate / 100m) : 0m;
            return new BillLine
            {
                BillId = bill.Id, Seq = seq++, ItemId = l.ItemId, Description = l.Description.Trim(), Quantity = l.Quantity, Price = l.Price, TaxCodeId = l.TaxCodeId, TaxAmount = tax,
                TaxRecoverable = l.TaxCodeId is not { } tc || taxes[tc].IsRecoverable, Net = net, AccountId = l.AccountId,
            };
        }).ToList();

        bill.PartyId = party.Id; bill.PartyName = party.Name; bill.VendorReference = string.IsNullOrEmpty(reference) ? null : reference; bill.BusinessUnitId = i.BusinessUnitId; bill.BillDate = i.BillDate;
        bill.DueDate = i.DueDate ?? i.BillDate.AddDays(party.PaymentTermsDays ?? 0);
        if (bill.DueDate < bill.BillDate) throw new FieldValidationException("dueDate", "The due date cannot be before the bill date.");
        bill.Notes = i.Notes;
        bill.Net = result.Sum(l => l.Net); bill.Tax = result.Sum(l => l.TaxAmount); bill.Total = bill.Net + bill.Tax;
        return result;
    }

    /// <summary>The unique index catches a duplicate vendor reference saved concurrently; it gets the same message as the up-front check.</summary>
    public async Task SaveAsync(Bill bill, CancellationToken ct)
    {
        try { await bills.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (dbErrors.IsUniqueViolation(ex, VendorReferenceIndex))
        {
            throw new ConflictException(DuplicateReferenceMessage(bill.VendorReference));
        }
    }

    /// <summary>Withdraws the bill's approval request while it is still pending; a decided request only loses its link.</summary>
    public async Task WithdrawPendingAsync(Bill bill, CancellationToken ct)
    {
        if (bill.ApprovalRequestId is not { } rid) return;
        var request = await approvalRequests.FindAsync(rid, ct);
        if (request?.Status == ApprovalStatus.Pending) await approvals.WithdrawAsync(rid, ct);
    }

    private static string DuplicateReferenceMessage(string? reference) => $"A bill with reference '{reference}' from this vendor is already recorded.";
}
