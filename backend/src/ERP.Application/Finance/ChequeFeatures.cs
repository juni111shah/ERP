using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record ChequeInput(
    Guid LegalEntityId, Guid PartyId, InstrumentKind Kind, string InstrumentNo, string? BankName, decimal Amount, DateOnly InstrumentDate, DateOnly? ReceivedOn, string? Notes,
    List<AllocationInput>? Allocations);

public sealed record ChequeDto(
    Guid Id, string Number, Guid LegalEntityId, InstrumentKind Kind, InstrumentDirection Direction, string InstrumentNo, string? BankName, decimal Amount, string CurrencyCode, DateOnly InstrumentDate,
    DateOnly? ReceivedOn, Guid? PartyId, string? PartyName, InstrumentStatus Status, Guid? BankAccountId, Guid? SettlementId, DateOnly? DepositedOn, DateOnly? ClearedOn, string? BounceReason,
    Guid? ReplacesInstrumentId, string? Notes, IReadOnlyList<AllocationDto> Allocations);

public sealed record ChequeSummaryDto(decimal InCustodyTotal, int InCustodyCount, decimal MaturedTotal, int MaturedCount, decimal Next7DaysTotal, decimal Next30DaysTotal, decimal DepositedTotal, int BouncedCount);

public sealed record GetCheque(Guid Id) : IQuery<ChequeDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ChequesRead; }
public sealed record GetChequeSummary(DateOnly AsOf) : IQuery<ChequeSummaryDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ChequesRead; }
/// <summary>Records a cheque received from a customer: Dr cheques in hand, Cr receivables, and settles the invoices it pays.</summary>
public sealed record RegisterCheque(ChequeInput Input) : ICommand<ChequeDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ChequesWrite; }
public sealed record CancelCheque(Guid Id) : ICommand<ChequeDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ChequesWrite; }
/// <summary>Takes the cheque to the bank. A post-dated cheque cannot be deposited before the date written on it.</summary>
public sealed record DepositCheque(Guid Id, Guid BankAccountId, DateOnly Date) : ICommand<ChequeDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ChequesApprove; }
/// <summary>The bank paid: Dr bank, Cr cheques in hand.</summary>
public sealed record ClearCheque(Guid Id, DateOnly Date) : ICommand<ChequeDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ChequesApprove; }
/// <summary>The bank returned it: the receipt is reversed and the invoices it settled are open again.</summary>
public sealed record BounceCheque(Guid Id, DateOnly Date, string Reason) : ICommand<ChequeDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ChequesApprove; }
/// <summary>A bounced cheque is replaced by a new one from the customer.</summary>
public sealed record ReplaceCheque(Guid Id, ChequeInput Replacement) : ICommand<ChequeDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ChequesApprove; }

public sealed class ChequeInputValidator : AbstractValidator<ChequeInput>
{
    public ChequeInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.InstrumentNo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.BankName).MaximumLength(100);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
public sealed class RegisterChequeValidator : AbstractValidator<RegisterCheque> { public RegisterChequeValidator() => RuleFor(x => x.Input).SetValidator(new ChequeInputValidator()); }
public sealed class ReplaceChequeValidator : AbstractValidator<ReplaceCheque> { public ReplaceChequeValidator() => RuleFor(x => x.Replacement).SetValidator(new ChequeInputValidator()); }
public sealed class BounceChequeValidator : AbstractValidator<BounceCheque> { public BounceChequeValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(300); }

internal sealed class ChequeHandlers(
    IRepository<PaymentInstrument> instruments, IRepository<Settlement> settlements, IRepository<SettlementAllocation> allocationRows, IReadRepository<Invoice> invoices, IReadRepository<Bill> bills,
    IReadRepository<Account> accounts, IReadRepository<Party> parties, ILegalEntityGuard guard, IPostingService posting, ISettlementService settlementService,
    INumberingService numbering, IUnitOfWork uow) :
    IRequestHandler<GetCheque, ChequeDto>,
    IRequestHandler<GetChequeSummary, ChequeSummaryDto>,
    IRequestHandler<RegisterCheque, ChequeDto>,
    IRequestHandler<CancelCheque, ChequeDto>,
    IRequestHandler<DepositCheque, ChequeDto>,
    IRequestHandler<ClearCheque, ChequeDto>,
    IRequestHandler<BounceCheque, ChequeDto>,
    IRequestHandler<ReplaceCheque, ChequeDto>
{
    public async Task<ChequeDto> Handle(GetCheque q, CancellationToken ct)
    {
        var i = await instruments.Query(new Specification<PaymentInstrument>(x => x.Id == q.Id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        return await ToDtoAsync(i, ct);
    }

    public async Task<ChequeSummaryDto> Handle(GetChequeSummary q, CancellationToken ct)
    {
        var rows = await instruments.Query(new Specification<PaymentInstrument>(i => i.Direction == InstrumentDirection.Received))
            .Select(i => new { i.Status, i.Amount, i.InstrumentDate }).ToListAsync(ct);
        var held = rows.Where(r => r.Status == InstrumentStatus.InCustody).ToList();
        return new ChequeSummaryDto(
            held.Sum(r => r.Amount), held.Count,
            held.Where(r => r.InstrumentDate <= q.AsOf).Sum(r => r.Amount), held.Count(r => r.InstrumentDate <= q.AsOf),
            held.Where(r => r.InstrumentDate > q.AsOf && r.InstrumentDate <= q.AsOf.AddDays(7)).Sum(r => r.Amount),
            held.Where(r => r.InstrumentDate > q.AsOf && r.InstrumentDate <= q.AsOf.AddDays(30)).Sum(r => r.Amount),
            rows.Where(r => r.Status == InstrumentStatus.Deposited).Sum(r => r.Amount), rows.Count(r => r.Status == InstrumentStatus.Bounced));
    }

    public Task<ChequeDto> Handle(RegisterCheque cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var cheque = await RegisterAsync(cmd.Input, null, token);
        await instruments.SaveChangesAsync(token);
        return await ToDtoAsync(cheque, token);
    }, ct);

    public async Task<ChequeDto> Handle(CancelCheque cmd, CancellationToken ct)
    {
        var c = await LoadAsync(cmd.Id, InstrumentStatus.InCustody, "Only a cheque still in custody can be cancelled.", ct);
        var s = await LoadSettlementAsync(c, ct);
        await settlementService.ReverseAsync(s, $"Cancel {c.Number}", ct);
        s.Status = DocumentStatus.Voided;
        c.Status = InstrumentStatus.Cancelled;
        await instruments.SaveChangesAsync(ct);
        return await ToDtoAsync(c, ct);
    }

    public async Task<ChequeDto> Handle(DepositCheque cmd, CancellationToken ct)
    {
        var c = await LoadAsync(cmd.Id, InstrumentStatus.InCustody, "Only a cheque in custody can be deposited.", ct);
        if (c.InstrumentDate > cmd.Date) throw new ConflictException($"This cheque is post-dated to {c.InstrumentDate:yyyy-MM-dd}; it cannot be deposited before then.");
        await RequireBankAsync(cmd.BankAccountId, ct);
        c.BankAccountId = cmd.BankAccountId; c.DepositedOn = cmd.Date; c.Status = InstrumentStatus.Deposited;
        await instruments.SaveChangesAsync(ct);
        return await ToDtoAsync(c, ct);
    }

    public async Task<ChequeDto> Handle(ClearCheque cmd, CancellationToken ct)
    {
        var c = await LoadAsync(cmd.Id, InstrumentStatus.Deposited, "Only a deposited cheque can be cleared.", ct);
        if (cmd.Date < c.DepositedOn) throw new FieldValidationException("date", "A cheque cannot clear before it was deposited.");
        var memo = $"Cheque {c.InstrumentNo} cleared";
        var held = await posting.AccountForRoleAsync("instruments.received", ct);
        await posting.PostAsync(new PostingRequest(c.LegalEntityId, cmd.Date, "cheque", c.Id, memo, c.Number,
            [new PostingLine(c.BankAccountId!.Value, c.Amount, 0, memo), new PostingLine(held, 0, c.Amount, memo, c.PartyId)], c.CurrencyCode), ct);
        c.ClearedOn = cmd.Date; c.Status = InstrumentStatus.Cleared;
        await instruments.SaveChangesAsync(ct);
        return await ToDtoAsync(c, ct);
    }

    public async Task<ChequeDto> Handle(BounceCheque cmd, CancellationToken ct)
    {
        var c = await instruments.Tracked(new Specification<PaymentInstrument>(x => x.Id == cmd.Id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (c.Status is not (InstrumentStatus.InCustody or InstrumentStatus.Deposited)) throw new ConflictException("Only a cheque in custody or deposited can bounce.");
        var s = await LoadSettlementAsync(c, ct);
        await settlementService.ReverseAsync(s, $"Bounced cheque {c.InstrumentNo}", ct);
        s.Status = DocumentStatus.Voided;
        c.Status = InstrumentStatus.Bounced; c.BounceReason = cmd.Reason.Trim();
        await instruments.SaveChangesAsync(ct);
        return await ToDtoAsync(c, ct);
    }

    public Task<ChequeDto> Handle(ReplaceCheque cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var old = await LoadAsync(cmd.Id, InstrumentStatus.Bounced, "Only a bounced cheque can be replaced.", token);
        if (cmd.Replacement.PartyId != old.PartyId) throw new FieldValidationException("partyId", "The replacement must come from the same customer.");
        var fresh = await RegisterAsync(cmd.Replacement, old.Id, token);
        old.Status = InstrumentStatus.Replaced;
        await instruments.SaveChangesAsync(token);
        return await ToDtoAsync(fresh, token);
    }, ct);

    private async Task<PaymentInstrument> RegisterAsync(ChequeInput i, Guid? replaces, CancellationToken ct)
    {
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var party = await parties.FindAsync(i.PartyId, ct) ?? throw new FieldValidationException("partyId", "Customer not found.");
        var no = i.InstrumentNo.Trim();
        var bank = i.BankName?.Trim();
        if (await instruments.AnyAsync(x => x.PartyId == party.Id && x.InstrumentNo == no && x.BankName == bank && x.Direction == InstrumentDirection.Received
                && x.Status != InstrumentStatus.Cancelled && x.Status != InstrumentStatus.Replaced && x.Status != InstrumentStatus.Bounced, ct))
            throw new ConflictException($"Cheque {no} from this customer is already registered.");

        var received = i.ReceivedOn ?? DateOnly.FromDateTime(DateTime.UtcNow);
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

    private async Task<PaymentInstrument> LoadAsync(Guid id, InstrumentStatus expected, string message, CancellationToken ct)
    {
        var c = await instruments.Tracked(new Specification<PaymentInstrument>(x => x.Id == id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (c.Status != expected) throw new ConflictException(message);
        return c;
    }

    private async Task<Settlement> LoadSettlementAsync(PaymentInstrument c, CancellationToken ct)
        => await settlements.Tracked(new Specification<Settlement>(x => x.Id == c.SettlementId).Include(x => x.Allocations)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    private async Task RequireBankAsync(Guid accountId, CancellationToken ct)
    {
        var a = await accounts.FindAsync(accountId, ct);
        if (a is null || a.Type != AccountType.Asset || !a.IsPostable || !a.IsActive) throw new FieldValidationException("bankAccountId", "Choose an active bank account.");
    }

    private async Task<ChequeDto> ToDtoAsync(PaymentInstrument c, CancellationToken ct)
    {
        var allocations = new List<AllocationDto>();
        if (c.SettlementId is { } sid)
        {
            var rows = await allocationRows.Query(new Specification<SettlementAllocation>(a => a.SettlementId == sid)).ToListAsync(ct);
            var invoiceIds = rows.Where(a => a.InvoiceId != null).Select(a => a.InvoiceId!.Value).ToList();
            var billIds = rows.Where(a => a.BillId != null).Select(a => a.BillId!.Value).ToList();
            var numbers = await invoices.Query(new Specification<Invoice>(i => invoiceIds.Contains(i.Id))).Select(i => new { i.Id, i.Number }).ToDictionaryAsync(i => i.Id, i => i.Number, ct);
            foreach (var b in await bills.Query(new Specification<Bill>(x => billIds.Contains(x.Id))).Select(x => new { x.Id, x.Number }).ToListAsync(ct)) numbers[b.Id] = b.Number;
            allocations = rows.Select(a => { var id = (a.InvoiceId ?? a.BillId)!.Value; return new AllocationDto(a.Id, id, numbers.GetValueOrDefault(id, "?"), a.Amount); }).ToList();
        }
        return new ChequeDto(c.Id, c.Number, c.LegalEntityId, c.Kind, c.Direction, c.InstrumentNo, c.BankName, c.Amount, c.CurrencyCode, c.InstrumentDate, c.ReceivedOn, c.PartyId, c.PartyName, c.Status,
            c.BankAccountId, c.SettlementId, c.DepositedOn, c.ClearedOn, c.BounceReason, c.ReplacesInstrumentId, c.Notes, allocations);
    }
}

public sealed record ChequeSummaryRow(
    Guid Id, string Number, InstrumentKind Kind, InstrumentDirection Direction, string InstrumentNo, string? BankName, Guid? PartyId, string? PartyName, decimal Amount, DateOnly InstrumentDate,
    InstrumentStatus Status, string CurrencyCode);
