using ERP.Application.Abstractions;
using ERP.Domain.Common;

namespace ERP.Application.Workflow;

public sealed record AttachmentOwnerDescriptor(string ReadPermission, string WritePermission);

public interface IAttachmentOwnerResolver
{
    string OwnerType { get; }
    Task<AttachmentOwnerDescriptor?> ResolveAsync(Guid ownerId, CancellationToken ct = default);
}

public sealed class EntityAttachmentOwnerResolver<TEntity>(
    IEntityStore entityStore,
    string ownerType,
    string readPermission,
    string writePermission,
    Func<TEntity, bool>? predicate = null) : IAttachmentOwnerResolver where TEntity : Entity
{
    public string OwnerType => ownerType;

    public async Task<AttachmentOwnerDescriptor?> ResolveAsync(Guid ownerId, CancellationToken ct = default)
    {
        if (predicate is not null)
        {
            var entity = await entityStore.FindAsync<TEntity>(ownerId, ct);
            if (entity is null || !predicate(entity)) return null;
            return new AttachmentOwnerDescriptor(readPermission, writePermission);
        }

        var exists = await entityStore.ExistsAsync(typeof(TEntity), ownerId, ct);
        return exists ? new AttachmentOwnerDescriptor(readPermission, writePermission) : null;
    }
}
