using ERP.Application.Portal;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ERP.Api.Controllers.Portal;

/// <summary>Customer and vendor portal sign-in with a one-time email code.</summary>
[Route("api/v1/portal/auth")]
public sealed class PortalAuthController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpPost("request-code")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> RequestCode(PortalCodeRequest r, CancellationToken ct)
    {
        await Mediator.Send(new RequestPortalCodeCommand(r.Workspace, r.Email, r.Kind), ct);
        return NoContent();
    }

    [HttpPost("verify")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public Task<PortalSession> Verify(PortalVerifyRequest r, CancellationToken ct)
        => Mediator.Send(new VerifyPortalCodeCommand(r.Workspace, r.Email, r.Code, r.Kind), ct);

    /// <summary>Workspace code and email; <c>kind</c> "customer" or "vendor" (optional).</summary>
    public sealed record PortalCodeRequest(string Workspace, string Email, string? Kind = null);

    /// <summary>Workspace code, email and the emailed code; <c>kind</c> "customer" or "vendor" (optional).</summary>
    public sealed record PortalVerifyRequest(string Workspace, string Email, string Code, string? Kind = null);
}
