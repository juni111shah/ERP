using System.Text.Json;
using ERP.Application.Security;
using ERP.Application.Settings;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Organization;

/// <summary>The workspace's settings (core and enabled modules).</summary>
[Route("api/v1/settings")]
public sealed class SettingsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.SettingsRead)]
    public Task<IReadOnlyList<SettingView>> List(CancellationToken ct) => Mediator.Send(new ListSettingsQuery(), ct);

    [HttpPut]
    [PermissionByRequest(typeof(UpdateSettingsCommand))] // core.settings.write, or <module>.settings.write for a module's keys
    public async Task<IActionResult> Update(Dictionary<string, JsonElement> values, CancellationToken ct)
    {
        await Mediator.Send(new UpdateSettingsCommand(values), ct);
        return NoContent();
    }
}
