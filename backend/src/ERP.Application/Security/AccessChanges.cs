namespace ERP.Application.Security;

/// <summary>
/// Makes a change to what people may do take effect at once for everyone it touches, without signing them out. Their open screens get an
/// "access-changed" event over SignalR and refresh the session, which reads the new permissions, so the menu, buttons and route guards
/// are worked out again straight away. When something was taken away (<c>narrowed</c>), access tokens issued before the change are also
/// refused from now on: the next API call refreshes silently, so a removed permission is enforced at once even for a screen that missed
/// the push. Call it after the change is saved.
/// </summary>
public interface IAccessChangeNotifier
{
    /// <summary>The access of these members of workspace <paramref name="orgId"/> changed (their roles, or a role they hold).</summary>
    Task UsersChangedAsync(Guid orgId, IEnumerable<Guid> userIds, string reason, bool narrowed, CancellationToken ct);

    /// <summary>
    /// What every member of workspace <paramref name="orgId"/> sees changed (a module or a capability was switched). Tokens stay valid:
    /// a disabled module's routes are already refused by the module gate.
    /// </summary>
    Task WorkspaceChangedAsync(Guid orgId, string reason, CancellationToken ct);
}

/// <summary>Why access changed, sent to the client with the "access-changed" event.</summary>
public static class AccessChangeReasons
{
    public const string RoleChanged = "role-changed";
    public const string RolesAssigned = "roles-assigned";
    public const string MemberDeactivated = "member-deactivated";
    public const string ModulesChanged = "modules-changed";
    public const string CapabilitiesChanged = "capabilities-changed";
    public const string IndustryChanged = "industry-changed";
}
