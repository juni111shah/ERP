using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Workflow;

/// <summary>Data access for in-app notifications. Implemented in ERP.Infrastructure/Repositories/Workflow/NotificationRepository.cs.</summary>
public interface INotificationRepository : IRepository<Notification>
{
    /// <summary>The latest <paramref name="take"/> notifications of <paramref name="userId"/>, newest first.</summary>
    Task<List<NotificationDto>> ListLatestForUserAsync(Guid? userId, int take, CancellationToken ct);

    /// <summary>One notification of <paramref name="userId"/>, tracked for changes. Throws NotFoundException (also for someone else's).</summary>
    Task<Notification> GetForUserForUpdateAsync(Guid id, Guid? userId, CancellationToken ct);

    /// <summary>The unread notifications of <paramref name="userId"/>, tracked for changes.</summary>
    Task<List<Notification>> ListUnreadForUserForUpdateAsync(Guid? userId, CancellationToken ct);
}
