using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Parties;
using ERP.Application.Workflow;
using ERP.Domain.Finance;
using ERP.Domain.Parties;

namespace ERP.Application.Finance;

/// <summary>
/// The steps shared by create, post and allocate payment: resolving the vendor and the bank account, submitting for approval,
/// numbering and posting (the part that never happens before an approval) and allocating to bills.
/// </summary>
public sealed class PaymentIssuing(
    ISettlementRepository settlements,
    ISettlementAllocationRepository allocationRows,
    IBillRepository bills,
    IPaymentInstrumentRepository instruments,
    IReadRepository<Account> accounts,
    IReadRepository<Party> parties,
    ILegalEntityGuard guard,
    IPostingService posting,
    INumberingService numbering,
    IApprovalService approvals,
    PaymentDtoBuilder dtos)
{
    /// <summary>The legal entity, the vendor and an active bank or cash account of the payment.</summary>
    public async Task<(ERP.Domain.Organizations.LegalEntity Entity, Party Party, Account Bank)> ResolveAsync(PaymentInput i, CancellationToken ct)
    {
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var party = await parties.FindAsync(i.PartyId, ct) ?? throw new FieldValidationException("partyId", "Vendor not found.");
        VendorRules.RequireVendor(party);
        var bank = await accounts.FindAsync(i.BankAccountId, ct);
        if (bank is null || !BankAccountRules.IsCashOrBank(bank)) throw new FieldValidationException("bankAccountId", "Choose an active bank or cash account.");
        return (entity, party, bank);
    }

    public async Task SubmitForApprovalAsync(Settlement s, CancellationToken ct)
    {
        var request = await approvals.SubmitAsync(ApprovalDocumentTypes.Payment, s.Id, $"Payment to {s.PartyName}", s.Amount, s.Reference, s.LegalEntityId, ct);
        s.ApprovalRequestId = request.Id;
        await settlements.SaveChangesAsync(ct);
    }

    /// <summary>Numbers the payment, settles the bills and posts it: the part that never happens before an approval.</summary>
    public async Task<PaymentDto> IssueAsync(Settlement settlement, PaymentInput i, ERP.Domain.Organizations.LegalEntity entity, Party party, Account bank, CancellationToken ct)
    {
        settlement.Number = await numbering.NextAsync("payment", entity.Id, i.Date, ct);
        var applied = await AllocateAsync(settlement, i.Allocations ?? [], ct);

        var ap = await posting.AccountForRoleAsync(SystemRoles.ApControl, ct);
        var memo = $"Payment to {party.Name}";
        var credit = bank.Id;
        if (i.Method == SettlementMethod.Instrument)
        {
            var cheque = new PaymentInstrument
            {
                LegalEntityId = entity.Id, Kind = (i.ChequeDate ?? i.Date) > i.Date ? InstrumentKind.PostDatedCheque : InstrumentKind.Cheque, Direction = InstrumentDirection.Issued, InstrumentNo = i.ChequeNo!.Trim(),
                BankName = i.BankName?.Trim(), Amount = i.Amount, CurrencyCode = entity.BaseCurrency, InstrumentDate = i.ChequeDate ?? i.Date, ReceivedOn = i.Date, PartyId = party.Id, PartyName = party.Name,
                Status = InstrumentStatus.Issued, BankAccountId = bank.Id, SettlementId = settlement.Id, Number = await numbering.NextAsync("instrument", entity.Id, i.Date, ct),
            };
            credit = await posting.AccountForRoleAsync("instruments.issued", ct);
            settlement.PaymentInstrumentId = cheque.Id;
            instruments.Add(cheque);
        }
        var journal = await posting.PostAsync(new PostingRequest(entity.Id, i.Date, "payment", settlement.Id, memo, settlement.Number,
            [new PostingLine(ap, i.Amount, 0, memo, party.Id), new PostingLine(credit, 0, i.Amount, memo)], entity.BaseCurrency), ct);
        settlement.JournalEntryId = journal.Id;
        settlement.Status = DocumentStatus.Issued;
        settlement.PendingInput = null;
        allocationRows.AddRange(applied);
        await settlements.SaveChangesAsync(ct);
        return await dtos.BuildAsync(settlement, ct, applied);
    }

    /// <summary>Validates and applies allocations to open bills of the payment's vendor; returns the new allocation rows (the caller adds them and saves).</summary>
    public async Task<List<SettlementAllocation>> AllocateAsync(Settlement s, List<BillAllocationInput> requested, CancellationToken ct)
    {
        if (requested.Count == 0) return [];
        if (requested.GroupBy(r => r.BillId).Any(g => g.Count() > 1)) throw new FieldValidationException("allocations", "A bill can appear only once.");
        if (requested.Any(r => r.Amount <= 0)) throw new FieldValidationException("allocations", "Allocation amounts must be positive.");
        if (requested.Sum(r => r.Amount) > s.Unallocated) throw new FieldValidationException("allocations", "The allocations exceed the unallocated amount of the payment.");
        var ids = requested.Select(r => r.BillId).ToList();
        var open = await bills.ListByIdsForUpdateAsync(ids, ct);
        var result = new List<SettlementAllocation>();
        foreach (var r in requested)
        {
            var bill = open.FirstOrDefault(b => b.Id == r.BillId) ?? throw new FieldValidationException("allocations", "A bill was not found.");
            CheckAllocation(s, bill, r);
            bill.Paid += r.Amount;
            BillStatus.Refresh(bill);
            s.Unallocated -= r.Amount;
            result.Add(new SettlementAllocation { SettlementId = s.Id, BillId = bill.Id, Amount = r.Amount });
        }
        return result;
    }

    /// <summary>The same checks without touching the bills: a payment going for approval should not fail only after someone approved it.</summary>
    public async Task CheckAllocationsAsync(Settlement s, List<BillAllocationInput> requested, CancellationToken ct)
    {
        if (requested.Count == 0) return;
        if (requested.GroupBy(r => r.BillId).Any(g => g.Count() > 1)) throw new FieldValidationException("allocations", "A bill can appear only once.");
        if (requested.Any(r => r.Amount <= 0)) throw new FieldValidationException("allocations", "Allocation amounts must be positive.");
        if (requested.Sum(r => r.Amount) > s.Unallocated) throw new FieldValidationException("allocations", "The allocations exceed the unallocated amount of the payment.");
        var ids = requested.Select(r => r.BillId).ToList();
        var open = await bills.ListByIdsAsync(ids, ct);
        foreach (var r in requested) CheckAllocation(s, open.FirstOrDefault(b => b.Id == r.BillId) ?? throw new FieldValidationException("allocations", "A bill was not found."), r);
    }

    private static void CheckAllocation(Settlement s, Bill bill, BillAllocationInput r)
    {
        if (bill.PartyId != s.PartyId || bill.LegalEntityId != s.LegalEntityId) throw new FieldValidationException("allocations", $"{bill.Number} is not a bill of this vendor.");
        if (bill.Status is not (DocumentStatus.Issued or DocumentStatus.PartlyPaid)) throw new FieldValidationException("allocations", $"{bill.Number} is not open.");
        if (bill.BillDate > s.Date) throw new FieldValidationException("allocations", $"{bill.Number} is dated after the payment.");
        if (r.Amount > bill.Total - bill.Paid) throw new FieldValidationException("allocations", $"{bill.Number} has only {bill.Total - bill.Paid:0.00} outstanding.");
    }
}
