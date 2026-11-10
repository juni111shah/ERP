using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>
/// Guards the public HTTP surface: every /api route with its HTTP method, who may call it, its module/capability gate and its
/// rate limit. A change to any of them must be deliberate, so the expected list is committed next to this file.
/// Regenerate it with the environment variable <c>ERP_UPDATE_ROUTE_SNAPSHOT=1</c> and review the diff.
/// </summary>
public class ApiRouteSnapshotTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string SnapshotFile = "api-routes.snapshot.txt";

    [Fact]
    public void The_api_surface_matches_the_committed_snapshot()
    {
        _ = f.CreateClient();
        var actual = Describe(f.Services.GetServices<EndpointDataSource>().SelectMany(s => s.Endpoints).OfType<RouteEndpoint>());
        var path = Path.Combine(ThisDirectory(), SnapshotFile);

        if (Environment.GetEnvironmentVariable("ERP_UPDATE_ROUTE_SNAPSHOT") == "1") File.WriteAllLines(path, actual);

        var expected = File.ReadAllLines(path);
        var missing = expected.Except(actual).ToList();
        var added = actual.Except(expected).ToList();
        Assert.True(missing.Count == 0 && added.Count == 0,
            $"The API surface changed.\nMissing ({missing.Count}):\n  {string.Join("\n  ", missing)}\nNew ({added.Count}):\n  {string.Join("\n  ", added)}");
    }

    [Fact]
    public void Every_api_route_is_a_controller_action()
    {
        _ = f.CreateClient();
        var minimal = f.Services.GetServices<EndpointDataSource>().SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is { } t && t.TrimStart('/').StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            .Where(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>() is null)
            .Select(e => e.DisplayName)
            .ToList();
        Assert.True(minimal.Count == 0, "Map these routes in a controller (ERP.Api/Controllers or a module's Controllers folder):\n" + string.Join("\n", minimal));
    }

    private static List<string> Describe(IEnumerable<RouteEndpoint> endpoints) => endpoints
        .Where(e => e.RoutePattern.RawText is { } t && t.TrimStart('/').StartsWith("api/", StringComparison.OrdinalIgnoreCase))
        .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["ANY"]).Select(method => Line(method, e)))
        .Distinct()
        .Order(StringComparer.Ordinal)
        .ToList();

    private static string Line(string method, RouteEndpoint e)
    {
        var route = e.RoutePattern.RawText!.Trim('/').ToLowerInvariant();
        var auth = e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy ?? a.Roles ?? "authenticated").Distinct().Order(StringComparer.Ordinal);
        var parts = new List<string> { $"{method} {route}", "auth=" + string.Join("+", auth) };
        if (e.Metadata.GetMetadata<IAllowAnonymous>() is not null) parts.Add("anonymous");
        if (Gate(e, "ModuleGate", "ModuleKey") is { } module) parts.Add("module=" + module);
        if (Gate(e, "CapabilityGate", "Capability") is { } capability) parts.Add("capability=" + capability);
        if (e.Metadata.Any(m => m.GetType().Name.StartsWith("AllowedInReadOnlyWorkspace", StringComparison.Ordinal))) parts.Add("read-only-ok");
        if (e.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName is { } limit) parts.Add("rate=" + limit);
        return string.Join(" | ", parts);
    }

    /// <summary>Reads a gate marker by name so the snapshot does not depend on how the marker type is declared.</summary>
    private static string? Gate(RouteEndpoint e, string typePrefix, string property) => e.Metadata
        .Where(m => m.GetType().Name.StartsWith(typePrefix, StringComparison.Ordinal))
        .Select(m => m.GetType().GetProperty(property)?.GetValue(m) as string)
        .LastOrDefault(v => v is not null);

    private static string ThisDirectory([CallerFilePath] string file = "") => Path.GetDirectoryName(file)!;
}
