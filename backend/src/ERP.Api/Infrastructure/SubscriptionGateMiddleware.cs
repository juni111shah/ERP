using ERP.Application.Abstractions;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using ERP.Modularity;

namespace ERP.Api.Infrastructure;

/// <summary>
/// Holds a workspace to its subscription. Read-only: viewing still works and changes answer 402. Blocked: everything answers 402.
/// Signing in, the caller's own session and the billing pages always work, so a client can see why and put it right.
/// Data is never touched, and the platform console is never gated.
/// </summary>
public sealed class SubscriptionGateMiddleware(RequestDelegate next)
{
    private static readonly string[] AlwaysOpen = ["/api/v1/auth", "/api/v1/me", "/api/v1/navigation", "/api/v1/billing", "/api/v1/platform-admin", "/api/v1/public", "/api/v1/platform"];

    public async Task InvokeAsync(HttpContext http)
    {
        var path = http.Request.Path;
        if (http.User.Identity?.IsAuthenticated != true || !path.StartsWithSegments("/api") || AlwaysOpen.Any(p => path.StartsWithSegments(p)))
        {
            await next(http);
            return;
        }
        var orgId = http.RequestServices.GetRequiredService<ICurrentUser>().OrgId;
        if (orgId is { } id)
        {
            var summary = await http.RequestServices.GetRequiredService<ISubscriptionAccess>().GetAsync(id, http.RequestAborted);
            if (summary.Access == SubscriptionAccessLevel.Blocked)
            {
                await ApiEnvelope.WriteErrorAsync(http, StatusCodes.Status402PaymentRequired, "Workspace unavailable",
                    summary.Status == SubscriptionStatus.Cancelled ? "This workspace's subscription is cancelled. Your data is kept. Contact us to restore it." : "This workspace is suspended. Your data is kept. See Subscription and billing, or contact us, to restore it.",
                    $"https://erp.local/problems/{SubscriptionProblems.BlockedType}");
                return;
            }
            if (summary.Access == SubscriptionAccessLevel.ReadOnly && http.GetEndpoint()?.Metadata.GetMetadata<AllowedInReadOnlyWorkspace>() is null && !HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method) && !HttpMethods.IsOptions(http.Request.Method))
            {
                await ApiEnvelope.WriteErrorAsync(http, StatusCodes.Status402PaymentRequired, "Changes are paused",
                    "This workspace is view-only until its subscription is brought up to date. See Subscription and billing.",
                    $"https://erp.local/problems/{SubscriptionProblems.ReadOnlyType}");
                return;
            }
        }
        await next(http);
    }
}
