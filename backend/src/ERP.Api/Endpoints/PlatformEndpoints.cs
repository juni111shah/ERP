using ERP.Application.Platform;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modularity.Features;
using MediatR;

namespace ERP.Api.Endpoints;

public static class PlatformEndpoints
{
    public static void MapPlatform(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/platform").WithTags("Platform");
        g.MapGet("/industries", (ISender sender, CancellationToken ct) => sender.Send(new ListIndustries(), ct)).AllowAnonymous();
        g.MapPost("/onboarding", async (OnboardingRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateWorkspace(request), ct);
            return Results.Created($"/api/v1/platform/workspaces/{result.OrgId}", result);
        }).AllowAnonymous().RequireRateLimiting("login");
    }

    public static void MapModules(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/modules").WithTags("Modules");
        g.MapGet("", (ISender sender, CancellationToken ct) => sender.Send(new ListModules(), ct)).RequirePermission(CorePermissions.SettingsRead);
        g.MapPost("/{key}/{action:regex(^(enable|disable)$)}", (string key, string action, ISender sender, CancellationToken ct) =>
            sender.Send(new SetModuleState(key, action == "enable"), ct)).RequirePermission(CorePermissions.ModulesManage);
    }
}
