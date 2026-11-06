using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Workflow;

/// <summary>Works out the chain of approval levels a request needs (pure).</summary>
public static class ApprovalRouting
{
    /// <summary>The default every policy used to store, before it named a real permission. Reads as the core approver permission, so old rows keep working.</summary>
    private const string LegacyDefaultPermission = "approvals.decide";

    public static string EffectivePermission(string? permission)
        => string.IsNullOrWhiteSpace(permission) || permission == LegacyDefaultPermission ? CorePermissions.ApprovalsDecide : permission;

    /// <summary>
    /// One step per level, lowest level first. Policies under the amount do not apply; of several at one level, the highest threshold reached wins (tiers).
    /// Without a matching policy (a request made because a setting asks for one, say) there is a single step for the core approver permission.
    /// </summary>
    public static IReadOnlyList<(int Level, string Permission)> Chain(IEnumerable<ApprovalPolicy> policies, decimal? amount)
    {
        var chain = policies
            .Where(p => p.IsActive && (p.MinAmount is null || (amount is not null && amount >= p.MinAmount)))
            .GroupBy(p => p.Level)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, EffectivePermission(g.OrderByDescending(p => p.MinAmount ?? 0m).First().ApproverPermission)))
            .ToList();
        return chain.Count > 0 ? chain : [(1, CorePermissions.ApprovalsDecide)];
    }
}

/// <summary>Maker-checker approvals. Segregation of duties lives on <see cref="ApprovalRequest"/>; this orchestrates persistence and notices.</summary>
public sealed class ApprovalService(
    IRepository<ApprovalRequest> requests, IReadRepository<ApprovalPolicy> policies, ICurrentUser user, IClock clock, INotifier notifier) : IApprovalService
{
    public Task<bool> RequiresApprovalAsync(string documentType, decimal? amount, CancellationToken ct = default)
        => policies.AnyAsync(p => p.IsActive && p.DocumentType == documentType && (p.MinAmount == null || (amount != null && amount >= p.MinAmount)), ct);

    public async Task<ApprovalRequest> SubmitAsync(string documentType, Guid? documentId, string subject, decimal? amount, string? justification, Guid? legalEntityId, CancellationToken ct = default)
    {
        var matching = await policies.Query(new Specification<ApprovalPolicy>(p => p.IsActive && p.DocumentType == documentType)).ToListAsync(ct);
        var request = new ApprovalRequest
        {
            DocumentType = documentType, DocumentId = documentId, Subject = subject, Amount = amount, Justification = justification,
            LegalEntityId = legalEntityId, RequestedBy = user.UserId ?? throw new ForbiddenException(), RequestedAt = clock.UtcNow,
        };
        request.Steps.AddRange(ApprovalRouting.Chain(matching, amount).Select(s => new ApprovalStep { ApprovalRequestId = request.Id, Level = s.Level, ApproverPermission = s.Permission }));
        requests.Add(request);
        await requests.SaveChangesAsync(ct);
        return request;
    }

    public async Task<ApprovalRequest> DecideAsync(Guid requestId, bool approve, string? comment, CancellationToken ct = default)
    {
        var request = await LoadAsync(requestId, ct);
        // Only the level whose turn it is can be decided, and it takes the permission of that level. A later level's approver has to wait.
        var current = request.CurrentStep;
        if (request.Status == ApprovalStatus.Pending && !user.HasPermission(ApprovalRouting.EffectivePermission(current?.ApproverPermission)))
            throw new ForbiddenException(current is not null && request.Steps.Any(s => s.Level > current.Level && user.HasPermission(ApprovalRouting.EffectivePermission(s.ApproverPermission)))
                ? $"Level {current.Level} of this request has to decide first."
                : "You do not have permission to decide this request.");
        request.Decide(user.UserId!.Value, approve, comment, clock.UtcNow);
        await requests.SaveChangesAsync(ct);
        var title = request.Status == ApprovalStatus.Pending ? $"Request approved at level {current!.Level}, waiting for the next level" : $"Request {(approve ? "approved" : "rejected")}";
        await notifier.NotifyUserAsync(request.RequestedBy, title, request.Subject, "/approvals", approve ? "success" : "warning", ct);
        return request;
    }

    public async Task<ApprovalRequest> WithdrawAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await LoadAsync(requestId, ct);
        request.Withdraw(user.UserId ?? Guid.Empty);
        await requests.SaveChangesAsync(ct);
        return request;
    }

    private async Task<ApprovalRequest> LoadAsync(Guid id, CancellationToken ct)
        => await requests.Tracked(new Specification<ApprovalRequest>(r => r.Id == id).Include(r => r.Steps)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
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
