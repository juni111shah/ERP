using Microsoft.AspNetCore.Authorization;

namespace ERP.Modularity.Web;

/// <summary>
/// Only callers holding <paramref name="permission"/> may use this action (or every action of this controller):
/// <c>[RequirePermission(CorePermissions.InvoicesRead)]</c>. Same policy as <c>RequirePermission</c> on a minimal endpoint.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute(PermissionEndpointExtensions.PolicyPrefix + permission)
{
    public string Permission { get; } = permission;
}
