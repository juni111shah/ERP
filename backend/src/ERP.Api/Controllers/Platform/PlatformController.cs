using ERP.Application.Platform;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ERP.Api.Controllers.Platform;

/// <summary>Public reference data for onboarding, and the older self sign-up route.</summary>
[Route("api/v1/platform")]
public sealed class PlatformController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("industries")]
    [AllowAnonymous]
    public Task<IReadOnlyList<IndustryView>> ListIndustries(CancellationToken ct) => Mediator.Send(new ListIndustriesQuery(), ct);

    [HttpGet("countries")]
    [AllowAnonymous]
    public Task<IReadOnlyList<CountryDto>> ListCountries(CancellationToken ct) => Mediator.Send(new ListCountriesQuery(), ct);

    [HttpGet("currencies")]
    [AllowAnonymous]
    public Task<IReadOnlyList<CurrencyOptionDto>> ListCurrencies(CancellationToken ct) => Mediator.Send(new ListCurrenciesQuery(), ct);

    [HttpPost("onboarding")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> CreateWorkspace(OnboardingRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new CreateWorkspaceCommand(request), ct);
        return Created($"/api/v1/platform/workspaces/{result.OrgId}", result);
    }
}
