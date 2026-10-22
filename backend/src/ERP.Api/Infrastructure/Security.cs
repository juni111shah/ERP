using ERP.Application.Security;
using ERP.Infrastructure.Services;
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
        if (policyName.StartsWith(PermissionEndpointExtensions.PolicyPrefix, StringComparison.Ordinal))
            return new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName[PermissionEndpointExtensions.PolicyPrefix.Length..])).Build();
        return await base.GetPolicyAsync(policyName);
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
