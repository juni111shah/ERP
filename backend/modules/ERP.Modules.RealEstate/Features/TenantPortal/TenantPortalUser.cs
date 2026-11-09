using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Modules.RealEstate.Features;

/// <summary>The tenant behind a portal request (shared by the tenant portal handlers).</summary>
internal static class TenantPortalUser
{
    /// <summary>The signed-in portal customer's party; any other caller is forbidden.</summary>
    public static Guid Me(ICurrentUser user) => user.PortalKind == "customer" && user.PortalPartyId is { } id ? id : throw new ForbiddenException();
}
