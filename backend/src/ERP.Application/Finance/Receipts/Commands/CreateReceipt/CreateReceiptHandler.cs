using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Finance;
using ERP.Domain.Parties;

namespace ERP.Application.Finance;

internal sealed class CreateReceiptHandler(
    ISettlementRepository settlements,
    ISettlementAllocationRepository allocationRows,
    ISettlementService settlementService,
    IReadRepository<Account> accounts,
    IReadRepository<Party> parties,
    ILegalEntityGuard guard,
    IPostingService posting,
    INumberingService numbering,
    ReceiptDtoBuilder dtos)
    : ICommandHandler<CreateReceiptCommand, ReceiptDto>
{
    public async Task<ReceiptDto> Handle(CreateReceiptCommand command, CancellationToken ct)
    {
        // 1. The legal entity, the customer and an active bank or cash account.
        var i = command.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var party = await parties.FindAsync(i.PartyId, ct) ?? throw new FieldValidationException("partyId", "Customer not found.");
        ERP.Application.Parties.CustomerRules.RequireCustomer(party);
        var bank = await accounts.FindAsync(i.BankAccountId, ct);
        if (bank is null || !BankAccountRules.IsCashOrBank(bank)) throw new FieldValidationException("bankAccountId", "Choose an active bank or cash account.");

        // 2. The receipt and its allocations.
        var settlement = new Settlement
        {
            LegalEntityId = entity.Id, Direction = InstrumentDirection.Received, PartyId = party.Id, PartyName = party.Name, Date = i.Date, Method = i.Method, BankAccountId = bank.Id,
            CurrencyCode = entity.BaseCurrency, Amount = i.Amount, Unallocated = i.Amount, Reference = i.Reference, Status = DocumentStatus.Issued, Number = "pending",
        };
        var applied = await settlementService.AllocateAsync(settlement, i.Allocations ?? [], ct);
        if (i.AutoAllocate) applied.AddRange(await settlementService.AutoAllocateAsync(settlement, ct)); // oldest invoice first, the rest stays on account

        // 3. Number and post it: Dr bank / cash, Cr receivables.
        settlement.Number = await numbering.NextAsync("receipt", entity.Id, i.Date, ct);
        var ar = await posting.AccountForRoleAsync(SystemRoles.ArControl, ct);
        var memo = $"Receipt from {party.Name}";
        var journal = await posting.PostAsync(new PostingRequest(entity.Id, i.Date, "receipt", settlement.Id, memo, settlement.Number,
            [new PostingLine(bank.Id, i.Amount, 0, memo), new PostingLine(ar, 0, i.Amount, memo, party.Id)], entity.BaseCurrency), ct);
        settlement.JournalEntryId = journal.Id;

        // 4. Save.
        settlements.Add(settlement);
        allocationRows.AddRange(applied);
        await settlements.SaveChangesAsync(ct);
        return await dtos.BuildAsync(settlement, ct, applied);
    }
}
