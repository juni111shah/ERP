using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Workflow;

internal sealed class ListAttachmentVaultHandler(IAttachmentRepository attachments, IEnumerable<IAttachmentOwnerResolver> resolvers, ICurrentUser user)
    : IQueryHandler<ListAttachmentVaultQuery, PagedResult<VaultRow>>
{
    public Task<PagedResult<VaultRow>> Handle(ListAttachmentVaultQuery query, CancellationToken ct)
    {
        // Same rule as download: the owner's read permission decides. An owner type nobody resolves stays hidden.
        var readable = resolvers.DistinctBy(r => r.OwnerType, StringComparer.OrdinalIgnoreCase)
            .Where(r => string.IsNullOrEmpty(r.ReadPermission) || user.HasPermission(r.ReadPermission))
            .Select(r => r.OwnerType.ToLowerInvariant()).ToList();
        return attachments.PageVaultAsync(query, readable, ct);
    }
}
