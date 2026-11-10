using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Workflow;

/// <summary>Finds the record an attachment belongs to and checks the caller may read (or write) it. Shared by the attachment handlers.</summary>
public sealed class AttachmentOwnerAccess(IEnumerable<IAttachmentOwnerResolver> resolvers, ICurrentUser user)
{
    public async Task<AttachmentOwnerDescriptor> ResolveAndAuthorizeAsync(string ownerType, Guid ownerId, bool isWrite, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ownerType))
            throw new FieldValidationException("ownerType", "Owner type and id are required.");
        if (ownerId == Guid.Empty)
            throw new FieldValidationException("ownerId", "Owner type and id are required.");

        var resolver = resolvers.FirstOrDefault(r => string.Equals(r.OwnerType, ownerType, StringComparison.OrdinalIgnoreCase));
        if (resolver is null)
            throw new FieldValidationException("ownerType", $"Unknown owner type '{ownerType}'.");

        var desc = await resolver.ResolveAsync(ownerId, ct);
        if (desc is null)
            throw new NotFoundException($"Owner record of type '{ownerType}' with ID '{ownerId}' was not found.");

        var requiredPermission = isWrite ? desc.WritePermission : desc.ReadPermission;
        if (!string.IsNullOrEmpty(requiredPermission) && !user.HasPermission(requiredPermission))
            throw new ForbiddenException();

        return desc;
    }
}
