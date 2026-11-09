using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>Vendor bills.</summary>
[Route("api/v1/finance/bills")]
public sealed class BillsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.BillsRead)]
    public Task<PagedResult<BillSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, BillSummaryDto.Projection, CorePermissions.BillsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(CorePermissions.BillsRead)]
    public Task<BillDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetBillByIdQuery(id), ct);

    [HttpPost]
    [RequirePermission(CorePermissions.BillsWrite)]
    public async Task<IActionResult> Create(BillInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateBillCommand(input), ct);
        return Created($"/api/v1/finance/bills/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(CorePermissions.BillsWrite)]
    public Task<BillDto> Update(Guid id, BillInput input, CancellationToken ct)
        => Mediator.Send(new UpdateBillCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(CorePermissions.BillsWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteBillCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/post")]
    [RequirePermission(CorePermissions.BillsPost)]
    public Task<BillDto> Post(Guid id, CancellationToken ct)
        => Mediator.Send(new PostBillCommand(id), ct);

    [HttpPost("{id:guid}/void")]
    [RequirePermission(CorePermissions.BillsVoid)]
    public Task<BillDto> Void(Guid id, VoidRequest? body, CancellationToken ct)
        => Mediator.Send(new VoidBillCommand(id, body?.Reason), ct);
}
