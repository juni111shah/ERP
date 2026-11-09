using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Workflow;

/// <summary>Data access for approval requests. Implemented in ERP.Infrastructure/Repositories/Workflow/ApprovalRequestRepository.cs.</summary>
public interface IApprovalRequestRepository : IRepository<ApprovalRequest>
{
    /// <summary>Pending requests made by someone other than <paramref name="userId"/>, newest first, steps included.</summary>
    Task<List<ApprovalRequest>> ListPendingRequestedByOthersAsync(Guid? userId, CancellationToken ct);

    /// <summary>The latest <paramref name="take"/> requests made by <paramref name="userId"/>, newest first, steps included.</summary>
    Task<List<ApprovalRequest>> ListRequestedByAsync(Guid? userId, int take, CancellationToken ct);

    /// <summary>One request with its steps, read-only. Throws NotFoundException.</summary>
    Task<ApprovalRequest> GetWithStepsAsync(Guid id, CancellationToken ct);
}
