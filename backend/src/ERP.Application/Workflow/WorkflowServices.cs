using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Workflow;

/// <summary>Maker-checker approvals. Segregation of duties lives on <see cref="ApprovalRequest"/>; this orchestrates persistence and notices.</summary>
public sealed class ApprovalService(
    IRepository<ApprovalRequest> requests, IReadRepository<ApprovalPolicy> policies, ICurrentUser user, IClock clock, INotifier notifier) : IApprovalService
{
    public Task<bool> RequiresApprovalAsync(string documentType, decimal? amount, CancellationToken ct = default)
        => policies.AnyAsync(p => p.IsActive && p.DocumentType == documentType && (p.MinAmount == null || (amount != null && amount >= p.MinAmount)), ct);

    public async Task<ApprovalRequest> SubmitAsync(string documentType, Guid? documentId, string subject, decimal? amount, string? justification, Guid? legalEntityId, CancellationToken ct = default)
    {
        var request = new ApprovalRequest
        {
            DocumentType = documentType, DocumentId = documentId, Subject = subject, Amount = amount, Justification = justification,
            LegalEntityId = legalEntityId, RequestedBy = user.UserId ?? throw new ForbiddenException(), RequestedAt = clock.UtcNow,
        };
        requests.Add(request);
        await requests.SaveChangesAsync(ct);
        return request;
    }

    public async Task<ApprovalRequest> DecideAsync(Guid requestId, bool approve, string? comment, CancellationToken ct = default)
    {
        var request = await requests.GetForUpdateAsync(requestId, ct);
        if (!user.HasPermission(CorePermissions.ApprovalsDecide)) throw new ForbiddenException();
        request.Decide(user.UserId!.Value, approve, comment, clock.UtcNow);
        await requests.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(request.RequestedBy, $"Request {(approve ? "approved" : "rejected")}", request.Subject, "/approvals", approve ? "success" : "warning", ct);
        return request;
    }

    public async Task<ApprovalRequest> WithdrawAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await requests.GetForUpdateAsync(requestId, ct);
        request.Withdraw(user.UserId ?? Guid.Empty);
        await requests.SaveChangesAsync(ct);
        return request;
    }
}

/// <summary>Stores an in-app notification and pushes it to the user's open sessions.</summary>
public sealed class Notifier(IRepository<Notification> notifications, INotificationPusher pusher) : INotifier
{
    public async Task NotifyUserAsync(Guid userId, string title, string? body = null, string? link = null, string severity = "info", CancellationToken ct = default)
    {
        var n = new Notification { UserId = userId, Title = title, Body = body, Link = link, Severity = severity };
        notifications.Add(n);
        await notifications.SaveChangesAsync(ct);
        await pusher.PushAsync(userId, new { n.Id, n.Title, n.Body, n.Link, n.Severity }, ct);
    }
}
