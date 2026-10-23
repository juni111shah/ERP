using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Features;

// ---- Brokerage: commission invoice and agent payout ----------------------------------------------------------------------------

/// <summary>Raises and issues the commission invoice to the client (Dr receivables, Cr brokerage income and output tax); the deal becomes Invoiced.</summary>
public sealed record InvoiceDealCommission(Guid Id) : ICommand<DealDto>, IRequirePermission { public string RequiredPermission => P.DealsInvoice; }

/// <summary>Pays the agent and team lead shares from a bank account (Dr commission expense, Cr bank). Only after the client has paid.</summary>
public sealed record PayDealAgent(Guid Id, Guid BankAccountId, DateOnly Date) : ICommand<DealDto>, IRequirePermission { public string RequiredPermission => P.DealsPayAgent; }

internal sealed class DealFinanceHandlers(
    IRepository<BrokerageDeal> deals, IReadRepository<Account> accounts, IInvoicingService invoicing, IPostingService posting, ICustomFieldService fields, IUnitOfWork uow, IClock clock) :
    IRequestHandler<InvoiceDealCommission, DealDto>,
    IRequestHandler<PayDealAgent, DealDto>,
    INotificationHandler<InvoicePaymentChanged>,
    INotificationHandler<InvoiceVoided>
{
    public const string SourceType = "realestate.deal";

    public Task<DealDto> Handle(InvoiceDealCommission cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var deal = await deals.GetForUpdateAsync(cmd.Id, token);
        if (deal.ClientPartyId is not { } client) throw new ConflictException("Link the client to a customer record before invoicing the commission.");
        if (deal.GrossCommission <= 0) throw new ConflictException("This deal has no commission to invoice.");
        var income = await posting.AccountForRoleAsync("realestate.income.brokerage", token);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var description = $"Brokerage commission {deal.Number} ({deal.DealType.ToString().ToLowerInvariant()}{(string.IsNullOrWhiteSpace(deal.UnitReference) ? "" : ", " + deal.UnitReference)})";
        var draft = new InvoiceDraft(deal.LegalEntityId, client, today, today, [new InvoiceLineDraft(description, 1, deal.GrossCommission, deal.TaxCodeId, income)], SourceType: SourceType, SourceId: deal.Id, Notes: deal.Number);
        var invoice = await invoicing.BuildAsync(draft, token);
        await invoicing.IssueAsync(invoice, token);
        deal.MarkInvoiced(invoice.Id);
        await deals.SaveChangesAsync(token);
        return DealDto.From(deal, await fields.ReadAsync(Docs.Deal, deal, token));
    }, ct);

    public Task<DealDto> Handle(PayDealAgent cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var deal = await deals.GetForUpdateAsync(cmd.Id, token);
        if (deal.Stage != DealStage.Collected) throw new ConflictException("The agent can be paid only after the client has paid the commission invoice.");
        decimal Share(decimal pct) => Math.Round(deal.GrossCommission * pct / 100m, 2, MidpointRounding.AwayFromZero);
        var payout = Share(deal.AgentSharePercent) + Share(deal.TeamLeadSharePercent);
        if (payout <= 0) throw new ConflictException("This deal has no agent or team lead share to pay.");
        var bank = await accounts.FindAsync(cmd.BankAccountId, token);
        if (bank is null || bank.Type != AccountType.Asset || !bank.IsPostable || !bank.IsActive) throw new FieldValidationException("bankAccountId", "Choose an active bank account.");
        var expense = await posting.AccountForRoleAsync("realestate.expense.commission", token);
        var memo = $"Commission payout {deal.Number}";
        var journal = await posting.PostAsync(new PostingRequest(deal.LegalEntityId, cmd.Date, SourceType, deal.Id, memo, deal.Number,
            [new PostingLine(expense, payout, 0, memo), new PostingLine(bank.Id, 0, payout, memo)], deal.CurrencyCode), token);
        deal.MarkAgentPaid(journal.Id, cmd.Date);
        await deals.SaveChangesAsync(token);
        return DealDto.From(deal, await fields.ReadAsync(Docs.Deal, deal, token));
    }, ct);

    /// <summary>The client paid (or a payment was taken back): move the deal between Invoiced and Collected.</summary>
    public async Task Handle(InvoicePaymentChanged n, CancellationToken ct)
    {
        if (n.SourceType != SourceType || n.SourceId is not { } id) return;
        var deal = await deals.Tracked(new Specification<BrokerageDeal>(d => d.Id == id && d.CommissionInvoiceId == n.InvoiceId)).FirstOrDefaultAsync(ct);
        if (deal is null) return;
        if (n.IsPaid) deal.MarkCollected(); else deal.ReopenCollected();
        await deals.SaveChangesAsync(ct);
    }

    public async Task Handle(InvoiceVoided n, CancellationToken ct)
    {
        if (n.SourceType != SourceType || n.SourceId is not { } id) return;
        var deal = await deals.Tracked(new Specification<BrokerageDeal>(d => d.Id == id && d.CommissionInvoiceId == n.InvoiceId)).FirstOrDefaultAsync(ct);
        if (deal is null) return;
        deal.ReleaseInvoice();
        await deals.SaveChangesAsync(ct);
    }
}

// ---- Security deposits -------------------------------------------------------------------------------------------------------

public sealed record DepositTransactionDto(Guid Id, DateOnly Date, DepositKind Kind, decimal Amount, string? Description);
public sealed record DepositDto(Guid LeaseId, decimal Agreed, decimal Received, decimal Applied, decimal Held, IReadOnlyList<DepositTransactionDto> Transactions);
public sealed record ArrearsInput(Guid InvoiceId, decimal Amount);
public sealed record SettleDepositInput(DateOnly Date, decimal Damages, string? DamagesDescription, List<ArrearsInput>? Arrears, decimal Refund, Guid? RefundBankAccountId);

public sealed record GetSecurityDeposit(Guid LeaseId) : IQuery<DepositDto>, IRequirePermission { public string RequiredPermission => P.LeasesRead; }
/// <summary>The tenant pays the security deposit: Dr bank, Cr deposits held.</summary>
public sealed record RecordSecurityDeposit(Guid LeaseId, Guid BankAccountId, DateOnly Date, decimal Amount) : ICommand<DepositDto>, IRequirePermission { public string RequiredPermission => P.DepositsManage; }
/// <summary>On move-out: damages become income, arrears settle the tenant's open invoices, the rest is refunded from a bank account.</summary>
public sealed record SettleSecurityDeposit(Guid LeaseId, SettleDepositInput Input) : ICommand<DepositDto>, IRequirePermission { public string RequiredPermission => P.DepositsManage; }
/// <summary>Rolls the held deposit over to the renewal lease without any cash moving.</summary>
public sealed record TransferSecurityDeposit(Guid FromLeaseId, Guid ToLeaseId) : ICommand<DepositDto>, IRequirePermission { public string RequiredPermission => P.DepositsManage; }

public sealed class RecordSecurityDepositValidator : AbstractValidator<RecordSecurityDeposit>
{
    public RecordSecurityDepositValidator() { RuleFor(x => x.Amount).GreaterThan(0); RuleFor(x => x.BankAccountId).NotEmpty(); }
}
public sealed class SettleSecurityDepositValidator : AbstractValidator<SettleSecurityDeposit>
{
    public SettleSecurityDepositValidator()
    {
        RuleFor(x => x.Input.Damages).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.Refund).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.DamagesDescription).MaximumLength(300);
        RuleFor(x => x.Input.RefundBankAccountId).NotEmpty().When(x => x.Input.Refund > 0).WithMessage("Choose the bank account the refund is paid from.");
        RuleForEach(x => x.Input.Arrears).ChildRules(a => a.RuleFor(y => y.Amount).GreaterThan(0));
    }
}

internal sealed class DepositHandlers(
    IRepository<DepositTransaction> transactions, IReadRepository<Lease> leases, IReadRepository<Account> accounts, IRepository<Settlement> settlements, IRepository<SettlementAllocation> allocationRows,
    IReadRepository<Party> parties, ISettlementService settlementService, IPostingService posting, INumberingService numbering, IUnitOfWork uow) :
    IRequestHandler<GetSecurityDeposit, DepositDto>,
    IRequestHandler<RecordSecurityDeposit, DepositDto>,
    IRequestHandler<SettleSecurityDeposit, DepositDto>,
    IRequestHandler<TransferSecurityDeposit, DepositDto>
{
    private const string Role = "realestate.deposits";

    public async Task<DepositDto> Handle(GetSecurityDeposit q, CancellationToken ct) => await BuildAsync(await leases.GetAsync(q.LeaseId, ct), ct);

    public Task<DepositDto> Handle(RecordSecurityDeposit cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var lease = await leases.GetAsync(cmd.LeaseId, token);
        if (lease.Status == LeaseStatus.Cancelled) throw new ConflictException("A cancelled lease cannot take a deposit.");
        var before = await BuildAsync(lease, token);
        if (lease.SecurityDepositAmount > 0 && before.Received + cmd.Amount > lease.SecurityDepositAmount)
            throw new FieldValidationException("amount", $"The agreed deposit is {lease.SecurityDepositAmount:0.00}; {before.Received:0.00} has already been received.");
        await RequireBankAsync(cmd.BankAccountId, "bankAccountId", token);
        var deposits = await posting.AccountForRoleAsync(Role, token);
        var memo = $"Security deposit, lease {lease.Number}";
        var journal = await posting.PostAsync(new PostingRequest(lease.LegalEntityId, cmd.Date, "deposit", lease.Id, memo, lease.Number,
            [new PostingLine(cmd.BankAccountId, cmd.Amount, 0, memo), new PostingLine(deposits, 0, cmd.Amount, memo, lease.LesseePartyId)], lease.CurrencyCode), token);
        transactions.Add(new DepositTransaction { LegalEntityId = lease.LegalEntityId, LeaseId = lease.Id, Date = cmd.Date, Kind = DepositKind.Received, Amount = cmd.Amount, Description = "Deposit received", JournalEntryId = journal.Id });
        await transactions.SaveChangesAsync(token);
        return await BuildAsync(lease, token);
    }, ct);

    public Task<DepositDto> Handle(SettleSecurityDeposit cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var i = cmd.Input;
        var lease = await leases.GetAsync(cmd.LeaseId, token);
        if (lease.Status is not (LeaseStatus.Ended or LeaseStatus.Terminated or LeaseStatus.Renewed)) throw new ConflictException("Settle the deposit once the tenancy has ended or been terminated.");
        var arrears = i.Arrears ?? [];
        var arrearsTotal = arrears.Sum(a => a.Amount);
        var total = i.Damages + arrearsTotal + i.Refund;
        if (total <= 0) throw new ConflictException("Enter damages, arrears or a refund to settle.");
        var held = (await BuildAsync(lease, token)).Held;
        if (total > held) throw new ConflictException($"Only {held:0.00} is held; the settlement is {total:0.00}.");
        var deposits = await posting.AccountForRoleAsync(Role, token);
        var rows = new List<DepositTransaction>();

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

        if (i.Damages > 0 || i.Refund > 0)
        {
            var lines = new List<PostingLine> { new(deposits, i.Damages + i.Refund, 0, "Deposit settlement", lease.LesseePartyId) };
            if (i.Damages > 0) lines.Add(new PostingLine(await posting.AccountForRoleAsync("realestate.income.damages", token), 0, i.Damages, i.DamagesDescription ?? "Damages deducted from deposit"));
            if (i.Refund > 0) { await RequireBankAsync(i.RefundBankAccountId!.Value, "refundBankAccountId", token); lines.Add(new PostingLine(i.RefundBankAccountId.Value, 0, i.Refund, "Deposit refund")); }
            var memo = $"Deposit settlement, lease {lease.Number}";
            var journal = await posting.PostAsync(new PostingRequest(lease.LegalEntityId, i.Date, "deposit", lease.Id, memo, lease.Number, lines, lease.CurrencyCode), token);
            if (i.Damages > 0) rows.Add(new DepositTransaction { LegalEntityId = lease.LegalEntityId, LeaseId = lease.Id, Date = i.Date, Kind = DepositKind.Damages, Amount = i.Damages, Description = i.DamagesDescription ?? "Damages", JournalEntryId = journal.Id });
            if (i.Refund > 0) rows.Add(new DepositTransaction { LegalEntityId = lease.LegalEntityId, LeaseId = lease.Id, Date = i.Date, Kind = DepositKind.Refund, Amount = i.Refund, Description = "Refunded to tenant", JournalEntryId = journal.Id });
        }
        transactions.AddRange(rows);
        await transactions.SaveChangesAsync(token);
        return await BuildAsync(lease, token);
    }, ct);

    public Task<DepositDto> Handle(TransferSecurityDeposit cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        if (cmd.FromLeaseId == cmd.ToLeaseId) throw new FieldValidationException("toLeaseId", "Choose a different lease.");
        var from = await leases.GetAsync(cmd.FromLeaseId, token);
        var to = await leases.GetAsync(cmd.ToLeaseId, token);
        if (from.LesseePartyId != to.LesseePartyId || from.LegalEntityId != to.LegalEntityId) throw new ConflictException("A deposit can only roll over to a lease of the same tenant and legal entity.");
        if (to.Status is LeaseStatus.Cancelled or LeaseStatus.Ended or LeaseStatus.Terminated) throw new ConflictException("The receiving lease is closed.");
        var held = (await BuildAsync(from, token)).Held;
        if (held <= 0) throw new ConflictException("There is no deposit to transfer.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Both leases keep the money in the same liability account, so only the deposit register changes.
        transactions.AddRange([
            new DepositTransaction { LegalEntityId = from.LegalEntityId, LeaseId = from.Id, Date = today, Kind = DepositKind.TransferOut, Amount = held, Description = $"Rolled over to {to.Number}" },
            new DepositTransaction { LegalEntityId = to.LegalEntityId, LeaseId = to.Id, Date = today, Kind = DepositKind.TransferIn, Amount = held, Description = $"Rolled over from {from.Number}" },
        ]);
        await transactions.SaveChangesAsync(token);
        return await BuildAsync(to, token);
    }, ct);

    private async Task<DepositDto> BuildAsync(Lease lease, CancellationToken ct)
    {
        var rows = await transactions.Query(new Specification<DepositTransaction>(t => t.LeaseId == lease.Id)).OrderBy(t => t.Date).ThenBy(t => t.CreatedAt).ToListAsync(ct);
        var received = rows.Where(r => r.Kind is DepositKind.Received or DepositKind.TransferIn).Sum(r => r.Amount);
        var applied = rows.Where(r => r.Kind is DepositKind.Damages or DepositKind.Arrears or DepositKind.Refund or DepositKind.TransferOut).Sum(r => r.Amount);
        return new DepositDto(lease.Id, lease.SecurityDepositAmount, received, applied, received - applied, rows.Select(r => new DepositTransactionDto(r.Id, r.Date, r.Kind, r.Amount, r.Description)).ToList());
    }

    private async Task RequireBankAsync(Guid id, string field, CancellationToken ct)
    {
        var a = await accounts.FindAsync(id, ct);
        if (a is null || a.Type != AccountType.Asset || !a.IsPostable || !a.IsActive) throw new FieldValidationException(field, "Choose an active bank account.");
    }
}

// ---- Termination credit notes ------------------------------------------------------------------------------------------------

public sealed record TerminationCreditDto(Guid InstalmentId, Guid CreditNoteId, string Number, decimal Amount, bool AppliedToInvoice);

/// <summary>For a terminated lease: credits rent that was invoiced beyond the termination date (whole instalments after it, the unused days of the one it falls in). Safe to run again.</summary>
public sealed record CreateTerminationCredits(Guid LeaseId) : ICommand<IReadOnlyList<TerminationCreditDto>>, IRequirePermission { public string RequiredPermission => P.LeasesTerminate; }

internal sealed class TerminationCreditHandlers(
    IReadRepository<Lease> leases, IRepository<RentInstalment> instalments, IRepository<Invoice> invoices, IInvoicingService invoicing, IPostingService posting, IUnitOfWork uow,
    IRepository<CreditApplication> creditApplications, ICurrentUser user) :
    IRequestHandler<CreateTerminationCredits, IReadOnlyList<TerminationCreditDto>>
{
    public Task<IReadOnlyList<TerminationCreditDto>> Handle(CreateTerminationCredits cmd, CancellationToken ct) => uow.InTransactionAsync<IReadOnlyList<TerminationCreditDto>>(async token =>
    {
        var lease = await leases.GetAsync(cmd.LeaseId, token);
        if (lease.Status != LeaseStatus.Terminated || lease.TerminationDate is not { } end) throw new ConflictException("Credit notes are raised for terminated leases.");
        var due = await instalments.Tracked(new Specification<RentInstalment>(i => i.LeaseId == lease.Id && i.Status == InstalmentStatus.Invoiced && i.CreditNoteId == null && i.InvoiceId != null && i.PeriodTo > end)).ToListAsync(token);
        var created = new List<TerminationCreditDto>();
        foreach (var i in due)
        {
            var original = await invoices.Tracked(new Specification<Invoice>(x => x.Id == i.InvoiceId).Include(x => x.Lines)).FirstAsync(token);
            if (original.Status is DocumentStatus.Voided or DocumentStatus.Draft) continue;
            var periodDays = i.PeriodTo.DayNumber - i.PeriodFrom.DayNumber + 1;
            var unusedDays = i.PeriodFrom > end ? periodDays : i.PeriodTo.DayNumber - end.DayNumber;
            var credit = i.PeriodFrom > end ? original.Net : Money.Round(original.Net * unusedDays / periodDays);
            if (credit <= 0) continue;
            var line = original.Lines.First();
            var description = $"Credit for rent after termination on {end:dd MMM yyyy}: {line.Description}";
            var draft = new InvoiceDraft(original.LegalEntityId, original.PartyId, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), [new InvoiceLineDraft(description, 1, credit, line.TaxCodeId, line.AccountId)],
                IsCreditNote: true, SourceType: "realestate.termination", SourceId: i.Id, Notes: lease.Number, CreditNoteForId: original.Id);
            var note = await invoicing.BuildAsync(draft, token);
            await invoicing.IssueAsync(note, token);
            // An unpaid invoice is reduced straight away; a paid one leaves the credit with the customer.
            var applied = false;
            if (original.Status is DocumentStatus.Issued or DocumentStatus.PartlyPaid)
            {
                var use = Math.Min(note.Total, original.Total - original.Paid);
                if (use > 0)
                {
                    note.Paid += use; original.Paid += use; SettlementStatus.Refresh(note); SettlementStatus.Refresh(original); applied = true;
                    CreditApplications.Record(creditApplications, note, original, use, note.InvoiceDate, user.UserId);
                }
            }
            i.CreditNoteId = note.Id;
            created.Add(new TerminationCreditDto(i.Id, note.Id, note.Number, note.Total, applied));
        }
        await instalments.SaveChangesAsync(token);
        return created;
    }, ct);
}
