using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Portal;

/// <summary>The party signed in to the portal. Anyone else (staff, or the other kind of portal user) is forbidden.</summary>
internal static class PortalCaller
{
    /// <summary>The signed-in party, whatever the kind (used by the profile).</summary>
    public static Guid AnyParty(ICurrentUser user) => user.PortalPartyId ?? throw new ForbiddenException();

    public static Guid Customer(ICurrentUser user) => user.PortalKind == "customer" && user.PortalPartyId is { } id ? id : throw new ForbiddenException();

    public static Guid Vendor(ICurrentUser user) => user.PortalKind == "vendor" && user.PortalPartyId is { } id ? id : throw new ForbiddenException();
}
