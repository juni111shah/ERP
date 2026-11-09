using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Workflow;
using ERP.Domain.Finance;
using ERP.Domain.Platform;

namespace ERP.Application.Finance;

internal sealed class PostPaymentHandler(
    ISettlementRepository settlements,
    PaymentIssuing issuing,
    IApprovalService approvals,
    IReadRepository<ApprovalRequest> approvalRequests,
    IUnitOfWork uow,
    PaymentDtoBuilder dtos)
    : ICommandHandler<PostPaymentCommand, PaymentDto>
{
    public Task<PaymentDto> Handle(PostPaymentCommand command, CancellationToken ct) => uow.InTransactionAsync(token => PostAsync(command, token), ct);

    private async Task<PaymentDto> PostAsync(PostPaymentCommand command, CancellationToken ct)
    {
        // 1. Only a payment waiting for approval is posted here.
        var s = await settlements.GetPaymentWithAllocationsForUpdateAsync(command.Id, ct);
        if (s.Status != DocumentStatus.PendingApproval) throw new ConflictException("Only a payment waiting for approval can be posted here.");

        // 2. Still covered by a policy: a rejected or withdrawn request goes again, a pending one blocks.
        if (await approvals.RequiresApprovalAsync(ApprovalDocumentTypes.Payment, s.Amount, ct))
        {
            var request = s.ApprovalRequestId is { } rid ? await approvalRequests.FindAsync(rid, ct) : null;
            if (request is null || request.Status is ApprovalStatus.Rejected or ApprovalStatus.Withdrawn)
            {
                await issuing.SubmitForApprovalAsync(s, ct);
                return await dtos.BuildAsync(s, ct);
            }
            if (request.Status == ApprovalStatus.Pending) throw new ConflictException("This payment is waiting for approval.");
        }

        // 3. Approved: number, allocate and post it from the input it was saved with.
        var i = System.Text.Json.JsonSerializer.Deserialize<PaymentInput>(s.PendingInput!)!;
        var (entity, party, bank) = await issuing.ResolveAsync(i, ct);
        return await issuing.IssueAsync(s, i, entity, party, bank, ct);
    }
}
