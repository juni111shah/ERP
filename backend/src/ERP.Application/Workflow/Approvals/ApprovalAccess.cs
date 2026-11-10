using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Workflow;

/// <summary>Rules shared by the approval queries.</summary>
internal static class ApprovalAccess
{
    /// <summary>Requests of no particular company are visible to everyone; others only to users allowed in that company.</summary>
    public static bool InAllowedEntity(ICurrentUser user, ApprovalRequest r)
        => r.LegalEntityId is not { } entityId || user.AllowedLegalEntityIds is not { } allowed || allowed.Contains(entityId);
}
