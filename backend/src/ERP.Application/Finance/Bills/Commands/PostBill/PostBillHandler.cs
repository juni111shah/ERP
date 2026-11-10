using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Finance;
using ERP.Domain.Platform;
using MediatR;

namespace ERP.Application.Finance;

internal sealed class PostBillHandler(
    IBillRepository bills,
    IReadRepository<ApprovalRequest> approvalRequests,
    IApprovalService approvals,
    ILegalEntityGuard guard,
    IPostingService posting,
    INumberingService numbering,
    IUnitOfWork uow,
    IPublisher publisher)
    : ICommandHandler<PostBillCommand, BillDto>
{
    public Task<BillDto> Handle(PostBillCommand command, CancellationToken ct) => uow.InTransactionAsync(token => PostAsync(command, token), ct);

    private async Task<BillDto> PostAsync(PostBillCommand command, CancellationToken ct)
    {
        // 1. Only a draft (or pending) bill with a total can be posted; the record it came from may refuse it.
        var bill = await bills.GetWithLinesForUpdateAsync(command.Id, ct);
        if (bill.Status is not (DocumentStatus.Draft or DocumentStatus.PendingApproval)) throw new ConflictException("Only a draft bill can be posted.");
        if (bill.Total <= 0) throw new ConflictException("A bill with a zero total cannot be posted.");
        await publisher.Publish(new BillPosting(bill.Id, bill.Lines.Select(l => new BillPostingLine(l.AccountId, l.Quantity)).ToList()), ct);

        // 2. Covered by an approval policy: submit (again after a rejection or withdrawal), or wait while the request is pending.
        if (await approvals.RequiresApprovalAsync("bill", bill.Total, ct))
        {
            var request = bill.ApprovalRequestId is { } rid ? await approvalRequests.FindAsync(rid, ct) : null;
            if (request is null || request.Status is ApprovalStatus.Rejected or ApprovalStatus.Withdrawn)
            {
                var submitted = await approvals.SubmitAsync("bill", bill.Id, $"Bill {bill.VendorReference ?? bill.Number} from {bill.PartyName}", bill.Total, null, bill.LegalEntityId, ct);
                bill.ApprovalRequestId = submitted.Id; bill.Status = DocumentStatus.PendingApproval;
                await bills.SaveChangesAsync(ct);
                return BillDto.From(bill);
            }
            if (request.Status == ApprovalStatus.Pending) throw new ConflictException("This bill is waiting for approval.");
        }

        // 3. The journal: Dr expenses (per account) and recoverable input tax, Cr payables.
        var entity = await guard.RequireAsync(bill.LegalEntityId, ct);
        var ap = await posting.AccountForRoleAsync(SystemRoles.ApControl, ct);
        var memo = $"Bill {bill.VendorReference} {bill.PartyName}".Trim();
        var lines = new List<PostingLine>();
        foreach (var g in bill.Lines.GroupBy(l => l.AccountId!.Value))
        {
            var amount = g.Sum(l => l.Net + (l.TaxRecoverable ? 0 : l.TaxAmount)); // tax that cannot be recovered is part of the cost
            lines.Add(amount >= 0 ? new PostingLine(g.Key, amount, 0, memo, BusinessUnitId: bill.BusinessUnitId) : new PostingLine(g.Key, 0, -amount, memo, BusinessUnitId: bill.BusinessUnitId)); // a purchase variance in the buyer's favour is a credit
        }
        var recoverable = bill.Lines.Where(l => l.TaxRecoverable).Sum(l => l.TaxAmount);
        if (recoverable > 0) lines.Add(new PostingLine(await posting.AccountForRoleAsync(SystemRoles.TaxInput, ct), recoverable, 0, memo));
        lines.Add(new PostingLine(ap, 0, bill.Total, memo, bill.PartyId));
        lines.RemoveAll(l => l.Debit == 0 && l.Credit == 0);

        // 4. Number, post and save.
        bill.Number = await numbering.NextAsync("bill", bill.LegalEntityId, bill.BillDate, ct);
        var journal = await posting.PostAsync(new PostingRequest(bill.LegalEntityId, bill.BillDate, "bill", bill.Id, memo, bill.VendorReference ?? bill.Number, lines, bill.CurrencyCode), ct);
        bill.JournalEntryId = journal.Id; bill.Status = DocumentStatus.Issued;
        await bills.SaveChangesAsync(ct);
        return BillDto.From(bill);
    }
}
