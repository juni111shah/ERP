using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class SettleSecurityDepositHandler(
    IRepository<DepositTransaction> transactions, IReadRepository<Lease> leases, IRepository<Settlement> settlements, IRepository<SettlementAllocation> allocationRows,
    IReadRepository<Party> parties, SecurityDepositLedger ledger, ISettlementService settlementService, IPostingService posting, INumberingService numbering, IUnitOfWork uow)
    : ICommandHandler<SettleSecurityDepositCommand, DepositDto>
{
    public Task<DepositDto> Handle(SettleSecurityDepositCommand cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. The tenancy must be over and the settlement within what is held.
        var i = cmd.Input;
        var lease = await leases.GetAsync(cmd.LeaseId, token);
        if (lease.Status is not (LeaseStatus.Ended or LeaseStatus.Terminated or LeaseStatus.Renewed)) throw new ConflictException("Settle the deposit once the tenancy has ended or been terminated.");
        var arrears = i.Arrears ?? [];
        var arrearsTotal = arrears.Sum(a => a.Amount);
        var total = i.Damages + arrearsTotal + i.Refund;
        if (total <= 0) throw new ConflictException("Enter damages, arrears or a refund to settle.");
        var held = (await ledger.BuildAsync(lease, token)).Held;
        if (total > held) throw new ConflictException($"Only {held:0.00} is held; the settlement is {total:0.00}.");
        var deposits = await posting.AccountForRoleAsync(SecurityDepositLedger.Role, token);
        var rows = new List<DepositTransaction>();

        // 2. Arrears.
        if (arrearsTotal > 0)
        {
            // Deposit applied to arrears: Dr deposits held, Cr receivables; the invoices are settled through a settlement so ageing and statuses follow.
            var party = await parties.GetAsync(lease.LesseePartyId, token);
            var ar = await posting.AccountForRoleAsync(SystemRoles.ArControl, token);
            var settlement = new Settlement
            {
                LegalEntityId = lease.LegalEntityId, Direction = InstrumentDirection.Received, PartyId = party.Id, PartyName = party.Name, Date = i.Date, Method = SettlementMethod.Other,
                CurrencyCode = lease.CurrencyCode, Amount = arrearsTotal, Unallocated = arrearsTotal, Reference = $"Security deposit applied, lease {lease.Number}", Status = DocumentStatus.Issued,
                Number = await numbering.NextAsync("receipt", lease.LegalEntityId, i.Date, token),
            };
            var applied = await settlementService.AllocateAsync(settlement, arrears.Select(a => new AllocationInput(a.InvoiceId, a.Amount)).ToList(), token);
            var memo = $"Deposit applied to arrears, lease {lease.Number}";
            var journal = await posting.PostAsync(new PostingRequest(lease.LegalEntityId, i.Date, "deposit", lease.Id, memo, settlement.Number,
                [new PostingLine(deposits, arrearsTotal, 0, memo, party.Id), new PostingLine(ar, 0, arrearsTotal, memo, party.Id)], lease.CurrencyCode), token);
            settlement.JournalEntryId = journal.Id;
            settlements.Add(settlement); allocationRows.AddRange(applied);
            rows.Add(new DepositTransaction { LegalEntityId = lease.LegalEntityId, LeaseId = lease.Id, Date = i.Date, Kind = DepositKind.Arrears, Amount = arrearsTotal, Description = "Applied to unpaid rent", JournalEntryId = journal.Id });
        }

        // 3. Damages and refund.
        if (i.Damages > 0 || i.Refund > 0)
        {
            var lines = new List<PostingLine> { new(deposits, i.Damages + i.Refund, 0, "Deposit settlement", lease.LesseePartyId) };
            if (i.Damages > 0) lines.Add(new PostingLine(await posting.AccountForRoleAsync("realestate.income.damages", token), 0, i.Damages, i.DamagesDescription ?? "Damages deducted from deposit"));
            if (i.Refund > 0) { await ledger.RequireBankAsync(i.RefundBankAccountId!.Value, "refundBankAccountId", token); lines.Add(new PostingLine(i.RefundBankAccountId.Value, 0, i.Refund, "Deposit refund")); }
            var memo = $"Deposit settlement, lease {lease.Number}";
            var journal = await posting.PostAsync(new PostingRequest(lease.LegalEntityId, i.Date, "deposit", lease.Id, memo, lease.Number, lines, lease.CurrencyCode), token);
            if (i.Damages > 0) rows.Add(new DepositTransaction { LegalEntityId = lease.LegalEntityId, LeaseId = lease.Id, Date = i.Date, Kind = DepositKind.Damages, Amount = i.Damages, Description = i.DamagesDescription ?? "Damages", JournalEntryId = journal.Id });
            if (i.Refund > 0) rows.Add(new DepositTransaction { LegalEntityId = lease.LegalEntityId, LeaseId = lease.Id, Date = i.Date, Kind = DepositKind.Refund, Amount = i.Refund, Description = "Refunded to tenant", JournalEntryId = journal.Id });
        }

        // 4. Register the movements, save.
        transactions.AddRange(rows);
        await transactions.SaveChangesAsync(token);
        return await ledger.BuildAsync(lease, token);
    }, ct);
}
