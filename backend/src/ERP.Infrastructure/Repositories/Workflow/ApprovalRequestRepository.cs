using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Workflow;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Workflow;

internal sealed class ApprovalRequestRepository(IRepository<ApprovalRequest> requests) : RepositoryBase<ApprovalRequest>(requests), IApprovalRequestRepository
{
    public Task<List<ApprovalRequest>> ListPendingRequestedByOthersAsync(Guid? userId, CancellationToken ct)
        => Query(new Specification<ApprovalRequest>(r => r.Status == ApprovalStatus.Pending && r.RequestedBy != userId).Include(r => r.Steps))
            .OrderByDescending(r => r.RequestedAt).ToListAsync(ct);

    public Task<List<ApprovalRequest>> ListRequestedByAsync(Guid? userId, int take, CancellationToken ct)
        => Query(new Specification<ApprovalRequest>(r => r.RequestedBy == userId).Include(r => r.Steps)).OrderByDescending(r => r.RequestedAt).Take(take).ToListAsync(ct);

    public async Task<ApprovalRequest> GetWithStepsAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<ApprovalRequest>(x => x.Id == id).Include(x => x.Steps)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
}
