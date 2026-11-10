using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Workflow;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Workflow;

internal sealed class AttachmentRepository(IRepository<Attachment> attachments) : RepositoryBase<Attachment>(attachments), IAttachmentRepository
{
    public Task<List<AttachmentDto>> ListForOwnerAsync(string ownerType, Guid ownerId, CancellationToken ct)
        => Query(new Specification<Attachment>(x => x.OwnerType.ToLower() == ownerType.ToLower() && x.OwnerId == ownerId))
            .Select(x => new AttachmentDto(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.CreatedAt, x.Category, x.Description, x.ExpiresOn)).ToListAsync(ct);

    public Task<PagedResult<VaultRow>> PageVaultAsync(PagedQuery query, List<string> readableOwnerTypes, CancellationToken ct)
        => PageAsync(query, a => new VaultRow(a.Id, a.OwnerType, a.OwnerId, a.FileName, a.Category, a.Description, a.ExpiresOn, a.CreatedAt),
            new Specification<Attachment>(a => readableOwnerTypes.Contains(a.OwnerType.ToLower())), ct);
}
