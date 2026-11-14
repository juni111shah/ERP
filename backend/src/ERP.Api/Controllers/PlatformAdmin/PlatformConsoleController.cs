using ERP.Api.Infrastructure;
using ERP.Application.Common;
using ERP.Application.Platform;
using ERP.Modularity.Features;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.PlatformAdmin;

/// <summary>The founder's console: overview, modules, settings, activity and the billing run. The policy is on the class, so no route here can be left open by accident.</summary>
[Route("api/v1/platform-admin")]
[Authorize(Policy = PlatformAdminRequirement.Policy)]
public sealed class PlatformConsoleController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("dashboard")]
    public Task<PlatformDashboard> GetDashboard(CancellationToken ct) => Mediator.Send(new GetPlatformDashboardQuery(), ct);

    [HttpGet("modules")]
    public Task<IReadOnlyList<PlatformModuleView>> ListModules(CancellationToken ct) => Mediator.Send(new ListPlatformModulesQuery(), ct);

    [HttpGet("settings")]
    public Task<PlatformSettingsView> GetSettings(CancellationToken ct) => Mediator.Send(new GetPlatformSettingsQuery(), ct);

    [PlatformManager]
    [HttpPut("settings")]
    public Task<PlatformSettingsView> UpdateSettings(PlatformSettingsInput input, CancellationToken ct) => Mediator.Send(new UpdatePlatformSettingsCommand(input), ct);

    [HttpGet("activity")]
    public Task<PagedResult<ActivityRow>> ListActivity([FromQuery] ListPlatformActivityQuery query, CancellationToken ct) => Mediator.Send(query, ct);

    [PlatformManager]
    [HttpPost("billing-run")]
    public Task<LifecycleResult> RunBilling(CancellationToken ct) => Mediator.Send(new RunBillingNowCommand(), ct);
}
