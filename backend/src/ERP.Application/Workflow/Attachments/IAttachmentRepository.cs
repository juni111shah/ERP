using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Workflow;

/// <summary>Data access for attachments. Implemented in ERP.Infrastructure/Repositories/Workflow/AttachmentRepository.cs.</summary>
public interface IAttachmentRepository : IRepository<Attachment>
{
    /// <summary>The attachments of one record (owner type compared case-insensitively).</summary>
    Task<List<AttachmentDto>> ListForOwnerAsync(string ownerType, Guid ownerId, CancellationToken ct);

    /// <summary>The document vault page: attachments of the given (lower-case) owner types only.</summary>
    Task<PagedResult<VaultRow>> PageVaultAsync(PagedQuery query, List<string> readableOwnerTypes, CancellationToken ct);
}
