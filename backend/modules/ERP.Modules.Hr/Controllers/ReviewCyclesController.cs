using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Performance review cycles.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/review-cycles")]
public sealed class ReviewCyclesController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    [RequirePermission(P.ReviewsRead)]
    public Task<PagedResult<ReviewCycleDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, ReviewCycleDto.Projection, P.ReviewsRead, ct);

    [HttpPost]
    [RequirePermission(P.ReviewsManage)]
    public async Task<IActionResult> Create(ReviewCycleInput input, CancellationToken ct)
        => Created($"{Base}/review-cycles", await Mediator.Send(new CreateReviewCycleCommand(input), ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(P.ReviewsManage)]
    public Task<ReviewCycleDto> Update(Guid id, ReviewCycleInput input, CancellationToken ct) => Mediator.Send(new UpdateReviewCycleCommand(id, input), ct);
}
