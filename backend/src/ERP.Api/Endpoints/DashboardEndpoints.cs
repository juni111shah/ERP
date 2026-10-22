using ERP.Application.Dashboard;
using ERP.Application.Search;
using MediatR;

namespace ERP.Api.Endpoints;

public static class DashboardEndpoints
{
    /// <summary>The overview: cards, charts and alerts from core and every enabled module, limited to what the caller may see.</summary>
    public static void MapDashboard(this RouteGroupBuilder api)
    {
        api.MapGet("/dashboard", (Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetDashboard(legalEntityId), ct)).RequireAuthorization().WithTags("Dashboard");
        api.MapGet("/search", (string q, ISender s, CancellationToken ct) => s.Send(new GlobalSearch(q), ct)).RequireAuthorization().WithTags("Search");
    }
}
