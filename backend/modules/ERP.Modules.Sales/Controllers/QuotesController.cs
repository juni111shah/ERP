using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Sales.Controllers;

/// <summary>Sales quotes.</summary>
[ModuleGate(SalesModule.Key)]
[Route("api/v1/m/sales/quotes")]
public sealed class QuotesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.QuotesRead)]
    public Task<PagedResult<QuoteSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, QuoteSummaryDto.Projection, P.QuotesRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.QuotesRead)]
    public Task<QuoteDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetQuoteQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.QuotesManage)]
    public async Task<IActionResult> Create(QuoteInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateQuoteCommand(input), ct);
        return Created($"/api/v1/m/sales/quotes/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.QuotesManage)]
    public Task<QuoteDto> Update(Guid id, QuoteInput input, CancellationToken ct)
        => Mediator.Send(new UpdateQuoteCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.QuotesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteQuoteCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [RequirePermission(P.QuotesManage)]
    public Task<QuoteDto> ChangeStatus(Guid id, ChangeStatusRequest body, CancellationToken ct)
        => Mediator.Send(new ChangeQuoteStatusCommand(id, body.Status), ct);

    [HttpPost("{id:guid}/convert-to-order")]
    [RequirePermission(P.OrdersManage)]
    public Task<OrderDto> ConvertToOrder(Guid id, CancellationToken ct)
        => Mediator.Send(new ConvertQuoteToOrderCommand(id), ct);

    /// <summary>The status to move the quote to.</summary>
    public sealed record ChangeStatusRequest(SalesQuoteStatus Status);
}
