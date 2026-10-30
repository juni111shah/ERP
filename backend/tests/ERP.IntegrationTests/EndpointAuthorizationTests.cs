using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>Every routed API endpoint must say who may call it: [Authorize]/RequireAuthorization or an explicit AllowAnonymous.</summary>
public class EndpointAuthorizationTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public void Every_api_endpoint_is_either_authorized_or_explicitly_anonymous()
    {
        _ = f.CreateClient();
        var sources = f.Services.GetServices<EndpointDataSource>();
        var api = sources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is { } t && t.TrimStart('/').StartsWith("api/", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(api.Count > 100, $"Only {api.Count} api endpoints found");
        var open = api
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null && !e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any())
            .Select(e => $"{string.Join(",", e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])} {e.RoutePattern.RawText}")
            .ToList();
        Assert.True(open.Count == 0, "Endpoints with no authorization metadata:\n" + string.Join("\n", open));
    }
}
