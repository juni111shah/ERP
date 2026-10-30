using ERP.Application.Dashboard;
using ERP.Application.Search;
using MediatR;

namespace ERP.Api.Endpoints;

public static class DashboardEndpoints
{
    /// <summary>
    /// The overview: cards, charts and alerts from core and every enabled module, limited to what the caller may see.
    /// Optional <c>from</c>/<c>to</c> (yyyy-MM-dd) set the reporting period; the default is the last year ending today.
    /// </summary>
    public static void MapDashboard(this RouteGroupBuilder api)
    {
        api.MapGet("/dashboard", (Guid? legalEntityId, DateOnly? from, DateOnly? to, ISender s, CancellationToken ct) => s.Send(new GetDashboard(legalEntityId, from, to), ct)).RequireAuthorization().WithTags("Dashboard");
        api.MapGet("/search", (string q, ISender s, CancellationToken ct) => s.Send(new GlobalSearch(q), ct)).RequireAuthorization().WithTags("Search");
    }
}
