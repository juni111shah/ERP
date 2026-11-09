using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Organization;

/// <summary>The workspace's legal entities (the accounting boundary).</summary>
[Route("api/v1/legal-entities")]
public sealed class LegalEntitiesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.EntitiesRead)]
    public Task<PagedResult<LegalEntityDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList<LegalEntity, LegalEntityDto>(query, "legalentity", LegalEntityDto.From, CorePermissions.EntitiesRead, ct);

    [HttpPost]
    [RequirePermission(CorePermissions.EntitiesWrite)]
    public async Task<IActionResult> Create(LegalEntityInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateLegalEntityCommand(input), ct);
        return Created($"/api/v1/legal-entities/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(CorePermissions.EntitiesWrite)]
    public Task<LegalEntityDto> Update(Guid id, LegalEntityInput input, CancellationToken ct) => Mediator.Send(new UpdateLegalEntityCommand(id, input), ct);
}
