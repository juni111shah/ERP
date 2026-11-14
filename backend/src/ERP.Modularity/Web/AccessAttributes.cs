using Microsoft.AspNetCore.Authorization;

namespace ERP.Modularity.Web;

/// <summary>
/// Any signed-in member of the workspace may call this action, because it only touches the caller's own things (profile, password,
/// notifications, own leave) or returns only what the caller's permissions allow (menu, dashboard cards, search hits).
/// <paramref name="reason"/> says which, so a reviewer can tell a deliberate choice from a forgotten <c>[RequirePermission]</c>.
/// A bare <c>[Authorize]</c> is not accepted on an API action (see the permission coverage tests).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class SignedInAttribute(string reason) : AuthorizeAttribute
{
    public string Reason { get; } = reason;
}

/// <summary>
/// The permission depends on what is asked (the target status, the record type, each setting key, the approval level), so the MediatR
/// request decides it: each request type implements <c>IRequirePermission</c> (checked by the pipeline) or <c>IAuthorizesInHandler</c>
/// (checked by its handler, item by item). The caller must still be signed-in staff.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PermissionByRequestAttribute(params Type[] requests) : AuthorizeAttribute
{
    public IReadOnlyList<Type> Requests { get; } = requests;
}
