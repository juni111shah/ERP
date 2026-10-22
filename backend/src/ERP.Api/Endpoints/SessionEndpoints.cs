using ERP.Modularity.Features;
using MediatR;

namespace ERP.Api.Endpoints;

public static class SessionEndpoints
{
    public static void MapSession(this RouteGroupBuilder api)
    {
        api.MapGet("/me", (ISender sender, CancellationToken ct) => sender.Send(new GetMe(), ct)).RequireAuthorization().WithTags("Session");
        api.MapGet("/navigation/menu", (ISender sender, CancellationToken ct) => sender.Send(new GetNavigationMenu(), ct)).RequireAuthorization().WithTags("Session");
    }
}
