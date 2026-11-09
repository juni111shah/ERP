using Microsoft.AspNetCore.Authorization;

namespace ERP.Modularity.Web;

/// <summary>
/// Only callers holding <paramref name="permission"/> may use this action (or every action of this controller):
/// <c>[RequirePermission(CorePermissions.InvoicesRead)]</c> (policy <c>perm:&lt;key&gt;</c>, see <see cref="PermissionPolicies"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute(PermissionPolicies.PolicyPrefix + permission)
{
    public string Permission { get; } = permission;
}
