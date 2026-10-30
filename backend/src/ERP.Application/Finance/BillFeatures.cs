using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Parties;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record BillLineInput(string Description, decimal Quantity, decimal Price, Guid AccountId, Guid? TaxCodeId = null, Guid? ItemId = null);
public sealed record BillInput(Guid LegalEntityId, Guid PartyId, string? VendorReference, DateOnly BillDate, DateOnly? DueDate, List<BillLineInput> Lines, string? Notes = null, Guid? BusinessUnitId = null);

public sealed record BillLineDto(Guid Id, int Seq, string Description, decimal Quantity, decimal Price, Guid? TaxCodeId, decimal TaxAmount, bool TaxRecoverable, decimal Net, Guid? AccountId);
public sealed record BillDto(
    Guid Id, string Number, Guid LegalEntityId, Guid PartyId, string? PartyName, string? VendorReference, DateOnly BillDate, DateOnly DueDate, string CurrencyCode, decimal Net, decimal Tax,
    decimal Total, decimal Paid, DocumentStatus Status, Guid? ApprovalRequestId, Guid? JournalEntryId, string? Notes, IReadOnlyList<BillLineDto> Lines,
    string? VoidReason = null, DateTime? VoidedAt = null, Guid? BusinessUnitId = null)
{
    public decimal Outstanding => Total - Paid;
}
public sealed record BillSummaryDto(Guid Id, string Number, Guid PartyId, string? PartyName, string? VendorReference, DateOnly BillDate, DateOnly DueDate, decimal Total, decimal Paid, DocumentStatus Status, string CurrencyCode)
{
    public decimal Outstanding => Total - Paid;
}

public sealed record GetBill(Guid Id) : IQuery<BillDto>, IRequirePermission { public string RequiredPermission => CorePermissions.BillsRead; }
public sealed record CreateBill(BillInput Input) : ICommand<BillDto>, IRequirePermission { public string RequiredPermission => CorePermissions.BillsWrite; }
public sealed record UpdateBill(Guid Id, BillInput Input) : ICommand<BillDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => CorePermissions.BillsWrite; }
public sealed record DeleteBill(Guid Id) : ICommand, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => CorePermissions.BillsWrite; }
/// <summary>Posts the bill (Dr expenses and input tax, Cr payables). When an approval policy covers the amount the bill waits for a decision first.</summary>
public sealed record PostBill(Guid Id) : ICommand<BillDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => CorePermissions.BillsPost; }
/// <summary>Reason is required (3-500 characters, see <see cref="VoidMemo"/>); it goes to the reversal memo and the audit log.</summary>
public sealed record VoidBill(Guid Id, string? Reason) : ICommand<BillDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => CorePermissions.BillsVoid; }

public sealed class VoidBillValidator : AbstractValidator<VoidBill>
{
    public VoidBillValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}

public sealed class BillInputValidator : AbstractValidator<BillInput>
{
    public BillInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.BillDate).MustBePlausibleDate();
        RuleFor(x => x.DueDate).MustBePlausibleDate();
        RuleFor(x => x).Must(x => x.DueDate is null || x.DueDate >= x.BillDate).WithName("dueDate").WithMessage("The due date cannot be before the bill date.");
        RuleFor(x => x.VendorReference).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.BusinessUnitId).NotEqual(Guid.Empty).When(x => x.BusinessUnitId is not null).WithMessage("Choose a business unit or leave it empty.");
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: > 0 }).WithMessage("A bill needs at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(400);
            l.RuleFor(x => x.Quantity).GreaterThan(0).MustHaveAtMostDecimals(4);
            l.RuleFor(x => x.Price).GreaterThanOrEqualTo(0).MustHaveAtMostDecimals(4);
            l.RuleFor(x => x.AccountId).NotEmpty().WithMessage("Choose the expense account.");
        });
    }
}
public sealed class CreateBillValidator : AbstractValidator<CreateBill> { public CreateBillValidator() => RuleFor(x => x.Input).SetValidator(new BillInputValidator()); }
public sealed class UpdateBillValidator : AbstractValidator<UpdateBill> { public UpdateBillValidator() => RuleFor(x => x.Input).SetValidator(new BillInputValidator()); }

internal sealed class BillHandlers(
    IRepository<Bill> bills, IRepository<BillLine> lineRows, IReadRepository<Party> parties, IReadRepository<TaxCode> taxCodes, IReadRepository<Account> accounts, IRepository<JournalEntry> journals,
    IReadRepository<ApprovalRequest> approvalRequests, IApprovalService approvals, ILegalEntityGuard guard, IPostingService posting, INumberingService numbering,
    IRepository<AuditEvent> audit, ICurrentUser user, IClock clock, IDbErrors dbErrors, IReadRepository<BusinessUnit> businessUnits, IReferenceCheck references, IUnitOfWork uow) :
    IRequestHandler<GetBill, BillDto>,
    IRequestHandler<CreateBill, BillDto>,
    IRequestHandler<UpdateBill, BillDto>,
    IRequestHandler<DeleteBill, Unit>,
    IRequestHandler<PostBill, BillDto>,
    IRequestHandler<VoidBill, BillDto>
{
    public async Task<BillDto> Handle(GetBill q, CancellationToken ct)
        => ToDto(await bills.Query(new Specification<Bill>(b => b.Id == q.Id).Include(b => b.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException());

    public async Task<BillDto> Handle(CreateBill cmd, CancellationToken ct)
    {
        var entity = await guard.RequireAsync(cmd.Input.LegalEntityId, ct);
        var bill = new Bill { LegalEntityId = entity.Id, Status = DocumentStatus.Draft, CurrencyCode = entity.BaseCurrency };
        bill.Number = "DRAFT-" + bill.Id.ToString("N")[^8..];
        bill.Lines.AddRange(await ApplyAsync(bill, cmd.Input, ct));
        bills.Add(bill);
        await SaveAsync(bill, ct);
        return ToDto(bill);
    }

    public Task<BillDto> Handle(UpdateBill cmd, CancellationToken ct) => uow.InTransactionAsync(token => UpdateBillCoreAsync(cmd, token), ct);

    private async Task<BillDto> UpdateBillCoreAsync(UpdateBill cmd, CancellationToken ct)
    {
        var bill = await LoadAsync(cmd.Id, ct);
        if (bill.Status is not (DocumentStatus.Draft or DocumentStatus.PendingApproval)) throw new ConflictException("Only a draft bill can be edited. Void it instead.");
        if (cmd.Input.LegalEntityId != bill.LegalEntityId) throw new FieldValidationException("legalEntityId", "The legal entity of a bill cannot change.");
        var fresh = await ApplyAsync(bill, cmd.Input, ct);
        foreach (var old in bill.Lines.ToList()) lineRows.Remove(old);
        lineRows.AddRange(fresh);
        await WithdrawPendingAsync(bill, ct); // an edit invalidates a pending approval request
        bill.Status = DocumentStatus.Draft; bill.ApprovalRequestId = null;
        await SaveAsync(bill, ct);
        bill.Lines.Clear(); bill.Lines.AddRange(fresh);
        return ToDto(bill);
    }

    public Task<Unit> Handle(DeleteBill cmd, CancellationToken ct) => uow.InTransactionAsync(token => DeleteBillCoreAsync(cmd, token), ct);

    private async Task<Unit> DeleteBillCoreAsync(DeleteBill cmd, CancellationToken ct)
    {
        var bill = await LoadAsync(cmd.Id, ct);
        if (bill.Status is not (DocumentStatus.Draft or DocumentStatus.PendingApproval)) throw new ConflictException("Only a draft bill can be deleted.");
        await WithdrawPendingAsync(bill, ct);
        bills.Remove(bill);
        await bills.SaveChangesAsync(ct);
        return Unit.Value;
    }

    /// <summary>Withdraws the bill's approval request while it is still pending; a decided request only loses its link.</summary>
    private async Task WithdrawPendingAsync(Bill bill, CancellationToken ct)
    {
        if (bill.ApprovalRequestId is not { } rid) return;
        var request = await approvalRequests.FindAsync(rid, ct);
        if (request?.Status == ApprovalStatus.Pending) await approvals.WithdrawAsync(rid, ct);
    }

    public Task<BillDto> Handle(PostBill cmd, CancellationToken ct) => uow.InTransactionAsync(token => PostBillCoreAsync(cmd, token), ct);

    private async Task<BillDto> PostBillCoreAsync(PostBill cmd, CancellationToken ct)
    {
        var bill = await LoadAsync(cmd.Id, ct);
        if (bill.Status is not (DocumentStatus.Draft or DocumentStatus.PendingApproval)) throw new ConflictException("Only a draft bill can be posted.");
        if (bill.Total <= 0) throw new ConflictException("A bill with a zero total cannot be posted.");

        if (await approvals.RequiresApprovalAsync("bill", bill.Total, ct))
        {
            var request = bill.ApprovalRequestId is { } rid ? await approvalRequests.FindAsync(rid, ct) : null;
            if (request is null || request.Status is ApprovalStatus.Rejected or ApprovalStatus.Withdrawn)
            {
                var submitted = await approvals.SubmitAsync("bill", bill.Id, $"Bill {bill.VendorReference ?? bill.Number} from {bill.PartyName}", bill.Total, null, bill.LegalEntityId, ct);
                bill.ApprovalRequestId = submitted.Id; bill.Status = DocumentStatus.PendingApproval;
                await bills.SaveChangesAsync(ct);
                return ToDto(bill);
            }
            if (request.Status == ApprovalStatus.Pending) throw new ConflictException("This bill is waiting for approval.");
        }

        var entity = await guard.RequireAsync(bill.LegalEntityId, ct);
        var ap = await posting.AccountForRoleAsync(SystemRoles.ApControl, ct);
        var memo = $"Bill {bill.VendorReference} {bill.PartyName}".Trim();
        var lines = new List<PostingLine>();
        foreach (var g in bill.Lines.GroupBy(l => l.AccountId!.Value))
            lines.Add(new PostingLine(g.Key, g.Sum(l => l.Net + (l.TaxRecoverable ? 0 : l.TaxAmount)), 0, memo)); // tax that cannot be recovered is part of the cost
        var recoverable = bill.Lines.Where(l => l.TaxRecoverable).Sum(l => l.TaxAmount);
        if (recoverable > 0) lines.Add(new PostingLine(await posting.AccountForRoleAsync(SystemRoles.TaxInput, ct), recoverable, 0, memo));
        lines.Add(new PostingLine(ap, 0, bill.Total, memo, bill.PartyId));
        lines.RemoveAll(l => l.Debit == 0 && l.Credit == 0);

        bill.Number = await numbering.NextAsync("bill", bill.LegalEntityId, bill.BillDate, ct);
        var journal = await posting.PostAsync(new PostingRequest(bill.LegalEntityId, bill.BillDate, "bill", bill.Id, memo, bill.VendorReference ?? bill.Number, lines, bill.CurrencyCode), ct);
        bill.JournalEntryId = journal.Id; bill.Status = DocumentStatus.Issued;
        await bills.SaveChangesAsync(ct);
        return ToDto(bill);
    }

    public Task<BillDto> Handle(VoidBill cmd, CancellationToken ct) => uow.InTransactionAsync(token => VoidBillCoreAsync(cmd, token), ct);

    private async Task<BillDto> VoidBillCoreAsync(VoidBill cmd, CancellationToken ct)
    {
        var bill = await LoadAsync(cmd.Id, ct);
        if (bill.Status != DocumentStatus.Issued || bill.Paid != 0) throw new ConflictException("Only a posted bill with nothing paid can be voided.");
        var journal = await journals.Tracked(new Specification<JournalEntry>(j => j.Id == bill.JournalEntryId).Include(j => j.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        await posting.ReverseTodayAsync(journal, VoidMemo.For(bill.Number, cmd.Reason), ct);
        bill.Status = DocumentStatus.Voided;
        VoidMemo.Stamp(bill, cmd.Reason, clock, user);
        VoidMemo.Audit(audit, user, clock, nameof(Bill), bill.Id, bill.Number, cmd.Reason!);
        await bills.SaveChangesAsync(ct);
        return ToDto(bill);
    }

    public const string VendorReferenceIndex = "IX_Bills_OrgId_PartyId_VendorReference";

    /// <summary>The unique index catches a duplicate vendor reference saved concurrently; it gets the same message as the up-front check.</summary>
    private async Task SaveAsync(Bill bill, CancellationToken ct)
    {
        try { await bills.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (dbErrors.IsUniqueViolation(ex, VendorReferenceIndex))
        {
            throw new ConflictException(DuplicateReferenceMessage(bill.VendorReference));
        }
    }

    private static string DuplicateReferenceMessage(string? reference) => $"A bill with reference '{reference}' from this vendor is already recorded.";

    private async Task<Bill> LoadAsync(Guid id, CancellationToken ct)
        => await bills.Tracked(new Specification<Bill>(b => b.Id == id).Include(b => b.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    private async Task<List<BillLine>> ApplyAsync(Bill bill, BillInput i, CancellationToken ct)
    {
        var party = await parties.FindAsync(i.PartyId, ct) ?? throw new FieldValidationException("partyId", "Vendor not found.");
        VendorRules.RequireVendor(party);
        var reference = i.VendorReference?.Trim();
        if (!string.IsNullOrEmpty(reference) && await bills.AnyAsync(b => b.PartyId == party.Id && b.VendorReference == reference && b.Id != bill.Id && b.Status != DocumentStatus.Voided, ct))
            throw new ConflictException(DuplicateReferenceMessage(reference));

        var accountIds = i.Lines.Select(l => l.AccountId).Distinct().ToList();
        var found = await accounts.Query(new Specification<Account>(a => accountIds.Contains(a.Id) && a.IsPostable && a.IsActive)).Select(a => a.Id).ToListAsync(ct);
        if (found.Count != accountIds.Count) throw new FieldValidationException("lines", "An account was not found or cannot be posted to.");
        var taxIds = i.Lines.Where(l => l.TaxCodeId is not null).Select(l => l.TaxCodeId!.Value).Distinct().ToList();
        var taxes = await taxCodes.Query(new Specification<TaxCode>(t => taxIds.Contains(t.Id) && t.IsActive)).Select(t => new { t.Id, t.Rate, t.IsRecoverable }).ToDictionaryAsync(t => t.Id, ct);
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

    private static BillDto ToDto(Bill b) => new(
        b.Id, b.Number, b.LegalEntityId, b.PartyId, b.PartyName, b.VendorReference, b.BillDate, b.DueDate, b.CurrencyCode, b.Net, b.Tax, b.Total, b.Paid, b.Status, b.ApprovalRequestId, b.JournalEntryId, b.Notes,
        b.Lines.OrderBy(l => l.Seq).Select(l => new BillLineDto(l.Id, l.Seq, l.Description, l.Quantity, l.Price, l.TaxCodeId, l.TaxAmount, l.TaxRecoverable, l.Net, l.AccountId)).ToList(),
        b.VoidReason, b.VoidedAt, b.BusinessUnitId);
}
