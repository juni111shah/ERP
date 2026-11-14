using ERP.Application.Abstractions;
using ERP.Application.Platform;
using ERP.Application.Security;
using ERP.Modularity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ERP.Api.Infrastructure;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>Turns policy names "perm:&lt;key&gt;" into permission requirements on the fly.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PermissionPolicies.PolicyPrefix, StringComparison.Ordinal))
            return new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
                .RequireAssertion(ctx => !ctx.User.HasClaim(c => c.Type == "portal_party"))
                .AddRequirements(new PermissionRequirement(policyName[PermissionPolicies.PolicyPrefix.Length..])).Build();
        return await base.GetPolicyAsync(policyName);
    }
}

/// <summary>The founder's console: read from the account on every request, so taking someone off the team works at once.</summary>
public sealed class PlatformAdminRequirement : IAuthorizationRequirement
{
    public const string Policy = "platform-admin";
}

public sealed class PlatformAdminHandler(ICurrentUser user, IPlatformDirectory directory) : AuthorizationHandler<PlatformAdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PlatformAdminRequirement requirement)
    {
        if (user.UserId is { } id && await directory.IsPlatformAdminAsync(id)) context.Succeed(requirement);
    }
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim(ErpClaims.Permission, requirement.Permission) || context.User.HasClaim(ErpClaims.Permission, CorePermissions.All))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>A right inside the founder's console (see <see cref="PlatformTeamRole"/>), read from the account on every request.</summary>
public sealed class PlatformRoleRequirement(PlatformTeamRole least) : IAuthorizationRequirement
{
    public PlatformTeamRole Least { get; } = least;
}

public sealed class PlatformRoleHandler(ICurrentUser user, IPlatformDirectory directory) : AuthorizationHandler<PlatformRoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PlatformRoleRequirement requirement)
    {
        if (user.UserId is { } id && await directory.PlatformRoleAsync(id) is { } role && role >= requirement.Least) context.Succeed(requirement);
    }
}

/// <summary>A console action that hurts when it goes wrong (suspend, plans, modules, voids, settings, the team...): managers and the owner only.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PlatformManagerAttribute() : AuthorizeAttribute(PlatformRights.ManagerPolicy);

/// <summary>A console action only the platform owner may take (the AI assistant, who is a manager).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PlatformOwnerAttribute() : AuthorizeAttribute(PlatformRights.OwnerPolicy);

/// <summary>
/// A console change any platform team member may make, with the reason (day-to-day work such as notes, trials and payments). Every write in
/// the console carries this, <see cref="PlatformManagerAttribute"/> or <see cref="PlatformOwnerAttribute"/>; a test holds them to it.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PlatformTeamActionAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
