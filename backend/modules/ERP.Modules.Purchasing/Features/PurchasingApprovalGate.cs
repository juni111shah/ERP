using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Modules.Purchasing.Features;

/// <summary>
/// Puts requisitions and orders through the approval engine when an approval policy covers them. With no matching policy nothing changes.
/// As with bills, once the approver has decided the document is approved by asking for that status again.
/// </summary>
internal sealed class PurchasingApprovalGate(IApprovalService approvals, IReadRepository<ApprovalRequest> requests, ISettingsService settings, ICurrentUser user)
{
    public const string AutoApproveSetting = "purchasing.autoApproveOrdersBelow";

    /// <param name="MayApprove">The document can go to approved now.</param>
    /// <param name="AutoApproved">A policy matched but the amount is under the auto-approve threshold.</param>
    public sealed record Outcome(bool MayApprove, Guid? RequestId, bool AutoApproved = false);

    /// <param name="approving">The caller asked for approved; a request still pending is then an error rather than a no-op.</param>
    public async Task<Outcome> CheckAsync(string documentType, Guid documentId, string subject, decimal amount, Guid legalEntityId, Guid? requestId, Guid? raisedBy, bool approving, CancellationToken ct)
    {
        if (!await approvals.RequiresApprovalAsync(documentType, amount, ct)) return new(true, requestId);
        if (documentType == Docs.PurchaseOrder && amount < (await settings.GetAsync<decimal?>(AutoApproveSetting, legalEntityId, ct) ?? 0m)) return new(true, requestId, AutoApproved: true);

        var request = requestId is { } id ? await requests.FindAsync(id, ct) : null;
        if (request is null || request.Status is ApprovalStatus.Rejected or ApprovalStatus.Withdrawn)
        {
            var submitted = await approvals.SubmitAsync(documentType, documentId, subject, amount, null, legalEntityId, ct);
            return new(false, submitted.Id);
        }
        if (request.Status == ApprovalStatus.Pending)
            return approving ? throw new ConflictException("This document is waiting for approval.") : new(false, request.Id);
        // The engine stops whoever submitted the request; this stops whoever raised the document, when someone else submitted it.
        if (request.DecidedBy == raisedBy) throw new ConflictException("Whoever raised a document cannot approve it. Edit it to send it for approval again.");
        return new(true, request.Id);
    }

    /// <summary>The document changed or went away: a request still pending no longer applies. Only its requester can withdraw it; otherwise it just stops counting, as the document forgets it.</summary>
    public async Task ReleaseAsync(Guid? requestId, CancellationToken ct)
    {
        if (requestId is not { } id) return;
        var request = await requests.FindAsync(id, ct);
        if (request?.Status == ApprovalStatus.Pending && request.RequestedBy == user.UserId) await approvals.WithdrawAsync(id, ct);
    }
}
