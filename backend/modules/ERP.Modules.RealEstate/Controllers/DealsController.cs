using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Brokerage deals: pipeline, commission invoice and agent payout.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/deals")]
public sealed class DealsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.DealsRead)]
    public Task<PagedResult<DealDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList<BrokerageDeal, DealDto>(query, Docs.Deal, DealDto.From, P.DealsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.DealsRead)]
    public Task<DealDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetDealQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.DealsCreate)]
    public async Task<IActionResult> Create(DealInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateDealCommand(input), ct);
        return Created($"/api/v1/m/realestate/deals/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.DealsEdit)]
    public Task<DealDto> Update(Guid id, DealInput input, CancellationToken ct)
        => Mediator.Send(new UpdateDealCommand(id, input), ct);

    [HttpPost("{id:guid}/invoice-commission")]
    [RequirePermission(P.DealsInvoice)]
    public Task<DealDto> InvoiceCommission(Guid id, CancellationToken ct)
        => Mediator.Send(new InvoiceDealCommissionCommand(id), ct);

    [HttpPost("{id:guid}/pay-agent")]
    [RequirePermission(P.DealsPayAgent)]
    public Task<DealDto> PayAgent(Guid id, PayAgentRequest b, CancellationToken ct)
        => Mediator.Send(new PayDealAgentCommand(id, b.BankAccountId, b.Date), ct);

    [HttpPost("{id:guid}/stage")]
    [PermissionByRequest(typeof(MoveDealStageCommand))]
    public Task<DealDto> MoveStage(Guid id, DealStageRequest body, CancellationToken ct)
        => Mediator.Send(new MoveDealStageCommand(id, body.Stage, body.LostReason), ct);

    public sealed record PayAgentRequest(Guid BankAccountId, DateOnly Date);
    public sealed record DealStageRequest(DealStage Stage, string? LostReason = null);
}
