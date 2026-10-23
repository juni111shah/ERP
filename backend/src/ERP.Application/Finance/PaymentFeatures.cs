using ERP.Domain.Platform;
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

public sealed record BillAllocationInput(Guid BillId, decimal Amount);
public sealed record PaymentInput(
    Guid LegalEntityId, Guid PartyId, DateOnly Date, SettlementMethod Method, Guid BankAccountId, decimal Amount, string? Reference, List<BillAllocationInput>? Allocations,
    string? ChequeNo = null, DateOnly? ChequeDate = null, string? BankName = null);

/// <summary>Label is set only for an allocation with no linked bill (BillId = Guid.Empty, BillNumber = "").</summary>
public sealed record BillAllocationDto(Guid Id, Guid BillId, string BillNumber, decimal Amount, string? Label = null);
public sealed record PaymentDto(
    Guid Id, string Number, Guid LegalEntityId, Guid PartyId, string? PartyName, DateOnly Date, SettlementMethod Method, Guid? BankAccountId, string CurrencyCode, decimal Amount, decimal Unallocated,
    string? Reference, DocumentStatus Status, Guid? PaymentInstrumentId, Guid? JournalEntryId, IReadOnlyList<BillAllocationDto> Allocations,
    string? InstrumentKind = null, string? ChequeNumber = null, DateOnly? ChequeDate = null, string? ChequeStatus = null, string? ChequeBankName = null);
public sealed record PaymentSummaryDto(Guid Id, string Number, Guid PartyId, string? PartyName, DateOnly Date, SettlementMethod Method, decimal Amount, decimal Unallocated, DocumentStatus Status, string CurrencyCode);

public sealed record GetPayment(Guid Id) : IQuery<PaymentDto>, IRequirePermission { public string RequiredPermission => CorePermissions.PaymentsRead; }
/// <summary>Pays a vendor: Dr payables, Cr bank / cash (or "cheques issued" for a cheque), settling bills at the same time.</summary>
public sealed record CreatePayment(PaymentInput Input) : ICommand<PaymentDto>, IRequirePermission { public string RequiredPermission => CorePermissions.PaymentsWrite; }
public sealed record AllocatePayment(Guid Id, List<BillAllocationInput> Allocations) : ICommand<PaymentDto>, IRequirePermission { public string RequiredPermission => CorePermissions.PaymentsWrite; }
/// <summary>Reason is required (3-500 characters, see <see cref="VoidMemo"/>); it goes to the reversal memo and the audit log.</summary>
public sealed record VoidPayment(Guid Id, string? Reason) : ICommand<PaymentDto>, IRequirePermission { public string RequiredPermission => CorePermissions.PaymentsWrite; }

public sealed class VoidPaymentValidator : AbstractValidator<VoidPayment>
{
    public VoidPaymentValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}
/// <summary>The vendor banked a cheque we issued: Dr cheques issued, Cr bank.</summary>
public sealed record ClearIssuedCheque(Guid InstrumentId, DateOnly Date) : ICommand<PaymentDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ChequesApprove; }

public sealed class PaymentInputValidator : AbstractValidator<PaymentInput>
{
    public PaymentInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.BankAccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.ChequeNo).NotEmpty().MaximumLength(50).When(x => x.Method == SettlementMethod.Instrument).WithMessage("Enter the cheque number.");
    }
}
public sealed class CreatePaymentValidator : AbstractValidator<CreatePayment> { public CreatePaymentValidator() => RuleFor(x => x.Input).SetValidator(new PaymentInputValidator()); }

internal sealed class PaymentHandlers(
    IRepository<Settlement> settlements, IRepository<SettlementAllocation> allocationRows, IRepository<Bill> bills, IRepository<PaymentInstrument> instruments, IRepository<JournalEntry> journals,
    IReadRepository<Account> accounts, IReadRepository<Party> parties, ILegalEntityGuard guard, IPostingService posting, INumberingService numbering, IUnitOfWork uow,
    IRepository<AuditEvent> audit, ICurrentUser user, IClock clock) :
    IRequestHandler<GetPayment, PaymentDto>,
    IRequestHandler<CreatePayment, PaymentDto>,
    IRequestHandler<AllocatePayment, PaymentDto>,
    IRequestHandler<VoidPayment, PaymentDto>,
    IRequestHandler<ClearIssuedCheque, PaymentDto>
{
    public async Task<PaymentDto> Handle(GetPayment q, CancellationToken ct)
        => await ToDtoAsync(await settlements.Query(new Specification<Settlement>(s => s.Id == q.Id && s.Direction == InstrumentDirection.Issued).Include(s => s.Allocations)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException(), ct);

    public Task<PaymentDto> Handle(CreatePayment cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, token);
        var party = await parties.FindAsync(i.PartyId, token) ?? throw new FieldValidationException("partyId", "Vendor not found.");
        var bank = await accounts.FindAsync(i.BankAccountId, token);
        if (bank is null || bank.Type != AccountType.Asset || !bank.IsPostable || !bank.IsActive) throw new FieldValidationException("bankAccountId", "Choose an active bank or cash account.");

        var settlement = new Settlement
        {
            LegalEntityId = entity.Id, Direction = InstrumentDirection.Issued, PartyId = party.Id, PartyName = party.Name, Date = i.Date, Method = i.Method, BankAccountId = bank.Id,
            CurrencyCode = entity.BaseCurrency, Amount = i.Amount, Unallocated = i.Amount, Reference = i.Reference, Status = DocumentStatus.Issued,
            Number = await numbering.NextAsync("payment", entity.Id, i.Date, token),
        };
        var applied = await AllocateAsync(settlement, i.Allocations ?? [], token);

        var ap = await posting.AccountForRoleAsync(SystemRoles.ApControl, token);
        var memo = $"Payment to {party.Name}";
        var credit = bank.Id;
        if (i.Method == SettlementMethod.Instrument)
        {
            var cheque = new PaymentInstrument
            {
                LegalEntityId = entity.Id, Kind = (i.ChequeDate ?? i.Date) > i.Date ? InstrumentKind.PostDatedCheque : InstrumentKind.Cheque, Direction = InstrumentDirection.Issued, InstrumentNo = i.ChequeNo!.Trim(),
                BankName = i.BankName?.Trim(), Amount = i.Amount, CurrencyCode = entity.BaseCurrency, InstrumentDate = i.ChequeDate ?? i.Date, ReceivedOn = i.Date, PartyId = party.Id, PartyName = party.Name,
                Status = InstrumentStatus.Issued, BankAccountId = bank.Id, SettlementId = settlement.Id, Number = await numbering.NextAsync("instrument", entity.Id, i.Date, token),
            };
            credit = await posting.AccountForRoleAsync("instruments.issued", token);
            settlement.PaymentInstrumentId = cheque.Id;
            instruments.Add(cheque);
        }
        var journal = await posting.PostAsync(new PostingRequest(entity.Id, i.Date, "payment", settlement.Id, memo, settlement.Number,
            [new PostingLine(ap, i.Amount, 0, memo, party.Id), new PostingLine(credit, 0, i.Amount, memo)], entity.BaseCurrency), token);
        settlement.JournalEntryId = journal.Id;
        settlements.Add(settlement);
        allocationRows.AddRange(applied);
        await settlements.SaveChangesAsync(token);
        return await ToDtoAsync(settlement, token, applied);
    }, ct);

    public async Task<PaymentDto> Handle(AllocatePayment cmd, CancellationToken ct)
    {
        var s = await LoadAsync(cmd.Id, ct);
        if (s.Status != DocumentStatus.Issued) throw new ConflictException("Only a posted payment can be allocated.");
        var applied = await AllocateAsync(s, cmd.Allocations, ct);
        allocationRows.AddRange(applied);
        await settlements.SaveChangesAsync(ct);
        return await ToDtoAsync(s, ct, s.Allocations.Concat(applied).ToList());
    }

    public async Task<PaymentDto> Handle(VoidPayment cmd, CancellationToken ct)
    {
        var s = await LoadAsync(cmd.Id, ct);
        if (s.Status != DocumentStatus.Issued) throw new ConflictException("This payment is already voided.");
        PaymentInstrument? cheque = null;
        if (s.PaymentInstrumentId is { } iid)
        {
            cheque = await instruments.Tracked(new Specification<PaymentInstrument>(x => x.Id == iid)).FirstOrDefaultAsync(ct);
            if (cheque?.Status == InstrumentStatus.Cleared) throw new ConflictException("The cheque has already cleared the bank, so the payment cannot be voided.");
        }
        var journal = await journals.Tracked(new Specification<JournalEntry>(j => j.Id == s.JournalEntryId).Include(j => j.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        await posting.ReverseTodayAsync(journal, VoidMemo.For(s.Number, cmd.Reason), ct);
        var ids = s.Allocations.Select(a => a.BillId!.Value).ToList();
        foreach (var bill in await bills.Tracked(new Specification<Bill>(b => ids.Contains(b.Id))).ToListAsync(ct))
        {
            bill.Paid -= s.Allocations.Where(a => a.BillId == bill.Id).Sum(a => a.Amount);
            BillStatus.Refresh(bill);
        }
        s.Status = DocumentStatus.Voided; s.Unallocated = 0;
        if (cheque is not null) cheque.Status = InstrumentStatus.Cancelled;
        VoidMemo.Audit(audit, user, clock, nameof(Settlement), s.Id, s.Number, cmd.Reason!);
        await settlements.SaveChangesAsync(ct);
        return await ToDtoAsync(s, ct);
    }

    public async Task<PaymentDto> Handle(ClearIssuedCheque cmd, CancellationToken ct)
    {
        var c = await instruments.Tracked(new Specification<PaymentInstrument>(x => x.Id == cmd.InstrumentId && x.Direction == InstrumentDirection.Issued)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (c.Status != InstrumentStatus.Issued) throw new ConflictException("Only an issued cheque that has not cleared can be cleared.");
        var memo = $"Cheque {c.InstrumentNo} cleared";
        var issued = await posting.AccountForRoleAsync("instruments.issued", ct);
        await posting.PostAsync(new PostingRequest(c.LegalEntityId, cmd.Date, "cheque", c.Id, memo, c.Number,
            [new PostingLine(issued, c.Amount, 0, memo, c.PartyId), new PostingLine(c.BankAccountId!.Value, 0, c.Amount, memo)], c.CurrencyCode), ct);
        c.ClearedOn = cmd.Date; c.Status = InstrumentStatus.Cleared;
        await instruments.SaveChangesAsync(ct);
        return await ToDtoAsync(await LoadAsync(c.SettlementId!.Value, ct), ct);
    }

    private async Task<List<SettlementAllocation>> AllocateAsync(Settlement s, List<BillAllocationInput> requested, CancellationToken ct)
    {
        if (requested.Count == 0) return [];
        if (requested.GroupBy(r => r.BillId).Any(g => g.Count() > 1)) throw new FieldValidationException("allocations", "A bill can appear only once.");
        if (requested.Any(r => r.Amount <= 0)) throw new FieldValidationException("allocations", "Allocation amounts must be positive.");
        if (requested.Sum(r => r.Amount) > s.Unallocated) throw new FieldValidationException("allocations", "The allocations exceed the unallocated amount of the payment.");
        var ids = requested.Select(r => r.BillId).ToList();
        var open = await bills.Tracked(new Specification<Bill>(b => ids.Contains(b.Id))).ToListAsync(ct);
        var result = new List<SettlementAllocation>();
        foreach (var r in requested)
        {
            var bill = open.FirstOrDefault(b => b.Id == r.BillId) ?? throw new FieldValidationException("allocations", "A bill was not found.");
            if (bill.PartyId != s.PartyId || bill.LegalEntityId != s.LegalEntityId) throw new FieldValidationException("allocations", $"{bill.Number} is not a bill of this vendor.");
            if (bill.Status is not (DocumentStatus.Issued or DocumentStatus.PartlyPaid)) throw new FieldValidationException("allocations", $"{bill.Number} is not open.");
            if (r.Amount > bill.Total - bill.Paid) throw new FieldValidationException("allocations", $"{bill.Number} has only {bill.Total - bill.Paid:0.00} outstanding.");
            bill.Paid += r.Amount;
            BillStatus.Refresh(bill);
            s.Unallocated -= r.Amount;
            result.Add(new SettlementAllocation { SettlementId = s.Id, BillId = bill.Id, Amount = r.Amount });
        }
        return result;
    }

    private async Task<Settlement> LoadAsync(Guid id, CancellationToken ct)
        => await settlements.Tracked(new Specification<Settlement>(x => x.Id == id && x.Direction == InstrumentDirection.Issued).Include(x => x.Allocations)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    private async Task<PaymentDto> ToDtoAsync(Settlement s, CancellationToken ct, IReadOnlyCollection<SettlementAllocation>? allocations = null)
    {
        var rows = allocations ?? s.Allocations;
        var ids = rows.Select(a => a.BillId!.Value).ToList();
        var numbers = await bills.Query(new Specification<Bill>(b => ids.Contains(b.Id))).Select(b => new { b.Id, b.Number }).ToDictionaryAsync(b => b.Id, b => b.Number, ct);
        var ins = await InstrumentInfo.LoadAsync(instruments, s.PaymentInstrumentId, ct);
        return new PaymentDto(s.Id, s.Number, s.LegalEntityId, s.PartyId, s.PartyName, s.Date, s.Method, s.BankAccountId, s.CurrencyCode, s.Amount, s.Unallocated, s.Reference, s.Status, s.PaymentInstrumentId, s.JournalEntryId,
            rows.Select(a => new BillAllocationDto(a.Id, a.BillId!.Value, numbers.GetValueOrDefault(a.BillId.Value, "?"), a.Amount)).ToList(),
            ins?.Kind, ins?.No, ins?.Date, ins?.Status, ins?.BankName);
    }
}
