using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Modules.Projects.Features;

/// <summary>The customer behind a portal sign-in; anyone else is refused. Shared by the portal handlers.</summary>
internal static class PortalCustomer
{
    public static Guid IdOf(ICurrentUser user) => user.PortalKind == "customer" && user.PortalPartyId is { } id ? id : throw new ForbiddenException();
}
