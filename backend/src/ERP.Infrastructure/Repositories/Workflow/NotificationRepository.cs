using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Workflow;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Workflow;

internal sealed class NotificationRepository(IRepository<Notification> notifications) : RepositoryBase<Notification>(notifications), INotificationRepository
{
    public Task<List<NotificationDto>> ListLatestForUserAsync(Guid? userId, int take, CancellationToken ct)
        => Query(new Specification<Notification>(x => x.UserId == userId)).OrderByDescending(x => x.CreatedAt).Take(take)
            .Select(x => new NotificationDto(x.Id, x.Title, x.Body, x.Link, x.Severity, x.CreatedAt, x.ReadAt)).ToListAsync(ct);

    public async Task<Notification> GetForUserForUpdateAsync(Guid id, Guid? userId, CancellationToken ct)
        => await Tracked(new Specification<Notification>(x => x.Id == id && x.UserId == userId)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public Task<List<Notification>> ListUnreadForUserForUpdateAsync(Guid? userId, CancellationToken ct)
        => Tracked(new Specification<Notification>(x => x.UserId == userId && x.ReadAt == null)).ToListAsync(ct);
}
