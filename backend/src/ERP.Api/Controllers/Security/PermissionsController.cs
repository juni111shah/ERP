using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Security;

/// <summary>Every permission that roles can grant.</summary>
[Route("api/v1/permissions")]
public sealed class PermissionsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.RolesRead)]
    public Task<IReadOnlyList<PermissionDefinition>> List(CancellationToken ct) => Mediator.Send(new ListPermissionsQuery(), ct);
}
