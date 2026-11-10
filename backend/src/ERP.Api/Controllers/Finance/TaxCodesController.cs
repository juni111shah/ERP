using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>Tax codes.</summary>
[Route("api/v1/finance/tax-codes")]
public sealed class TaxCodesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.TaxRead)]
    public Task<PagedResult<TaxCodeDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, TaxCodeDto.Projection, CorePermissions.TaxRead, ct);

    [HttpPost]
    [RequirePermission(CorePermissions.TaxWrite)]
    public async Task<IActionResult> Create(TaxCodeInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateTaxCodeCommand(input), ct);
        return Created($"/api/v1/finance/tax-codes/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(CorePermissions.TaxWrite)]
    public Task<TaxCodeDto> Update(Guid id, TaxCodeInput input, CancellationToken ct)
        => Mediator.Send(new UpdateTaxCodeCommand(id, input), ct);
}
