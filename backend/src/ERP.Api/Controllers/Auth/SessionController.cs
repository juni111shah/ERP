using ERP.Modularity.Features;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Auth;

/// <summary>The signed-in user's session: who they are, their workspace and their menu.</summary>
[Route("api/v1")]
[SignedIn("The caller's own session: profile, workspaces and a menu narrowed to their permissions")]
public sealed class SessionController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("me")]
    public Task<MeDto> GetMe(CancellationToken ct) => Mediator.Send(new GetMeQuery(), ct);

    [HttpGet("navigation/menu")]
    public Task<IReadOnlyList<MenuGroupDto>> GetNavigationMenu(CancellationToken ct) => Mediator.Send(new GetNavigationMenuQuery(), ct);
}
