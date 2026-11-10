using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Finance;
using ERP.Domain.Parties;

namespace ERP.Application.Finance;

/// <summary>Registers a cheque received from a customer with its receipt, allocations and journal; shared by register and replace cheque (the caller saves).</summary>
public sealed class ChequeRegistration(
    IPaymentInstrumentRepository instruments,
    ISettlementRepository settlements,
    ISettlementAllocationRepository allocationRows,
    IReadRepository<Party> parties,
    ILegalEntityGuard guard,
    IPostingService posting,
    ISettlementService settlementService,
    INumberingService numbering)
{
    /// <summary>Dr cheques in hand, Cr receivables; the receipt settles the invoices. <paramref name="replaces"/> is the bounced cheque it replaces.</summary>
    public async Task<PaymentInstrument> RegisterAsync(ChequeInput i, Guid? replaces, CancellationToken ct)
    {
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var party = await parties.FindAsync(i.PartyId, ct) ?? throw new FieldValidationException("partyId", "Customer not found.");
        var no = i.InstrumentNo.Trim();
        var bank = i.BankName?.Trim();
        if (await instruments.AnyAsync(x => x.PartyId == party.Id && x.InstrumentNo == no && x.BankName == bank && x.Direction == InstrumentDirection.Received
                && x.Status != InstrumentStatus.Cancelled && x.Status != InstrumentStatus.Replaced && x.Status != InstrumentStatus.Bounced, ct))
            throw new ConflictException($"Cheque {no} from this customer is already registered.");

        var received = i.ReceivedOn ?? DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        var cheque = new PaymentInstrument
        {
            LegalEntityId = entity.Id, Kind = i.Kind, Direction = InstrumentDirection.Received, InstrumentNo = no, BankName = bank, Amount = i.Amount, CurrencyCode = entity.BaseCurrency,
            InstrumentDate = i.InstrumentDate, ReceivedOn = received, PartyId = party.Id, PartyName = party.Name, Status = InstrumentStatus.InCustody, Notes = i.Notes,
            ReplacesInstrumentId = replaces, Number = await numbering.NextAsync("instrument", entity.Id, received, ct),
        };
        var settlement = new Settlement
        {
            LegalEntityId = entity.Id, Direction = InstrumentDirection.Received, PartyId = party.Id, PartyName = party.Name, Date = received, Method = SettlementMethod.Instrument,
            CurrencyCode = entity.BaseCurrency, Amount = i.Amount, Unallocated = i.Amount, Reference = $"Cheque {no}", Status = DocumentStatus.Issued, PaymentInstrumentId = cheque.Id,
            Number = await numbering.NextAsync("receipt", entity.Id, received, ct),
        };
        var applied = await settlementService.AllocateAsync(settlement, i.Allocations ?? [], ct);
        if (i.AutoAllocate) applied.AddRange(await settlementService.AutoAllocateAsync(settlement, ct));
        var held = await posting.AccountForRoleAsync("instruments.received", ct);
        var ar = await posting.AccountForRoleAsync(SystemRoles.ArControl, ct);
        var memo = $"Cheque {no} received from {party.Name}";
        var journal = await posting.PostAsync(new PostingRequest(entity.Id, received, "cheque", cheque.Id, memo, cheque.Number,
            [new PostingLine(held, i.Amount, 0, memo, party.Id), new PostingLine(ar, 0, i.Amount, memo, party.Id)], entity.BaseCurrency), ct);
        settlement.JournalEntryId = journal.Id;
        cheque.SettlementId = settlement.Id; cheque.JournalEntryId = journal.Id;
        instruments.Add(cheque);
        settlements.Add(settlement);
        allocationRows.AddRange(applied);
        return cheque;
    }
}
