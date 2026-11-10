using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Retail.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Retail.Controllers;

/// <summary>Retail promotions and promotion codes.</summary>
[ModuleGate(RetailModule.Key)]
[Route("api/v1/m/retail/promotions")]
public sealed class PromotionsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.PromotionsRead)]
    public Task<PagedResult<RetailPromotionDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, RetailPromotionDto.Projection, Permissions.PromotionsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.PromotionsRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var promo = await Mediator.Send(new GetPromotionByIdQuery(id), ct);
        return promo is null ? NotFound() : Ok(promo);
    }

    [HttpPost]
    [RequirePermission(Permissions.PromotionsManage)]
    public async Task<IActionResult> Create(CreatePromotionCommand command, CancellationToken ct)
    {
        var created = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/retail/promotions/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.PromotionsManage)]
    public Task<RetailPromotionDto> Update(Guid id, UpdatePromotionCommand command, CancellationToken ct)
        => Mediator.Send(command with { Id = id }, ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.PromotionsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeletePromotionCommand(id), ct);
        return NoContent();
    }

    [HttpPost("validate")]
    [RequirePermission(Permissions.PromotionsRead)]
    public Task<PromoValidationResultDto> Validate(ValidatePromoCodeQuery query, CancellationToken ct)
        => Mediator.Send(query, ct);
}
