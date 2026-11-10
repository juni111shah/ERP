using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Performance reviews: draft, submit, acknowledge.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/reviews")]
public sealed class ReviewsController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    [RequirePermission(P.ReviewsRead)]
    public Task<PagedResult<ReviewRow>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, ReviewRow.Projection, P.ReviewsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.ReviewsRead)]
    public Task<ReviewDto> Get(Guid id, CancellationToken ct) => Mediator.Send(new GetReviewQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.ReviewsManage)]
    public async Task<IActionResult> Create(ReviewInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateReviewCommand(input), ct);
        return Created($"{Base}/reviews/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.ReviewsManage)]
    public Task<ReviewDto> Update(Guid id, ReviewInput input, CancellationToken ct) => Mediator.Send(new UpdateReviewCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.ReviewsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteReviewCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    [RequirePermission(P.ReviewsManage)]
    public Task<ReviewDto> Submit(Guid id, CancellationToken ct) => Mediator.Send(new SubmitReviewCommand(id), ct);

    [HttpPost("{id:guid}/acknowledge")]
    [RequirePermission(P.ReviewsManage)]
    public Task<ReviewDto> Acknowledge(Guid id, CancellationToken ct) => Mediator.Send(new AcknowledgeReviewCommand(id), ct);
}
