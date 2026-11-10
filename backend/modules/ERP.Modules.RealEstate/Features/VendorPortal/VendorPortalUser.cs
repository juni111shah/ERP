using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Modules.RealEstate.Features;

/// <summary>The vendor behind a portal request (shared by the vendor portal handlers).</summary>
internal static class VendorPortalUser
{
    /// <summary>The signed-in portal vendor's party; any other caller is forbidden.</summary>
    public static Guid Me(ICurrentUser user) => user.PortalKind == "vendor" && user.PortalPartyId is { } id ? id : throw new ForbiddenException();
}
