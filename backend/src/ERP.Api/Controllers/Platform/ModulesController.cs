using ERP.Application.Security;
using ERP.Modularity.Features;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Platform;

/// <summary>The workspace's modules: which are installed, and switching them on or off.</summary>
[Route("api/v1/modules")]
public sealed class ModulesController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Toggle = "{key}/{action:regex(^(enable|disable)$)}";

    [HttpGet]
    [RequirePermission(CorePermissions.SettingsRead)]
    public Task<IReadOnlyList<ModuleView>> List(CancellationToken ct) => Mediator.Send(new ListModulesQuery(), ct);

    // {action} is the action name in MVC, so the one route is served by one action per value.
    [HttpPost(Toggle)]
    [ActionName("enable")]
    [RequirePermission(CorePermissions.ModulesManage)]
    public Task<ModuleState> Enable(string key, CancellationToken ct) => Mediator.Send(new SetModuleStateCommand(key, true), ct);

    [HttpPost(Toggle)]
    [ActionName("disable")]
    [RequirePermission(CorePermissions.ModulesManage)]
    public Task<ModuleState> Disable(string key, CancellationToken ct) => Mediator.Send(new SetModuleStateCommand(key, false), ct);
}
