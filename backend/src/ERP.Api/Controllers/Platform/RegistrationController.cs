using ERP.Application.Platform;
using ERP.Modularity;
using ERP.Modularity.Features;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ERP.Api.Controllers.Platform;

/// <summary>The public side: anyone may read the price list and register.</summary>
[Route("api/v1/public")]
[AllowAnonymous]
public sealed class RegistrationController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("signup-config")]
    public Task<SignupConfig> GetSignupConfig(CancellationToken ct) => Mediator.Send(new GetSignupConfigQuery(), ct);

    [HttpGet("legal")]
    public Task<LegalDocuments> GetLegalDocuments(CancellationToken ct) => Mediator.Send(new GetLegalDocumentsQuery(), ct);

    [HttpGet("modules")]
    public Task<IReadOnlyList<PlatformModuleView>> ListModules(CancellationToken ct) => Mediator.Send(new ListPlatformModulesQuery(), ct);

    [HttpGet("sign-in-key-available")]
    [EnableRateLimiting("signup-check")]
    public Task<SignInKeyAvailability> CheckSignInKey(string key, CancellationToken ct) => Mediator.Send(new CheckSignInKeyQuery(key), ct);

    [HttpPost("signups")]
    [EnableRateLimiting("login")]
    public Task<SignupResult> Register(SignupInput input, CancellationToken ct)
        => Mediator.Send(new RegisterWorkspaceCommand(input, HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

    [HttpPost("signups/verify")]
    [EnableRateLimiting("login")]
    public Task<SignupResult> VerifySignup(VerifySignupRequest input, CancellationToken ct) => Mediator.Send(new VerifySignupCommand(input.Token), ct);

    [HttpGet("content")]
    public Task<PublicContent> GetContent(CancellationToken ct) => Mediator.Send(new GetPublicContentQuery(), ct);

    [HttpGet("assistant/status")]
    public Task<BrandAssistantStatus> GetAssistantStatus(CancellationToken ct) => Mediator.Send(new GetBrandAssistantStatusQuery(), ct);

    [HttpPost("assistant/messages")]
    [EnableRateLimiting("assistant-public")]
    public IResult AskAssistant(BrandMessageInput input, CancellationToken ct) => new AssistantStreamResult(Mediator.CreateStream(new AskBrandAssistantCommand(input), ct));

    [HttpPost("assistant/leads")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> SubmitLead(BrandLeadInput input, CancellationToken ct)
    {
        await Mediator.Send(new SubmitBrandLeadCommand(input), ct);
        return NoContent();
    }

    public sealed record VerifySignupRequest(string Token);
}
