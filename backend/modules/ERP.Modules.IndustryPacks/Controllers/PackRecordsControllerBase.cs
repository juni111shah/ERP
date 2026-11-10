using ERP.Application.Common;
using ERP.Modularity.Web;
using ERP.Modules.IndustryPacks.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.IndustryPacks.Controllers;

/// <summary>
/// The same routes for every pack, under /api/v1/m/{key}, with the record type as a route value. One set per module keeps start-up light:
/// a set per type would compile hundreds of handlers. The permission of the type (read, manage, approve) is checked on every request in
/// the pipeline (<see cref="RecordRequest.RequiredPermission"/>), and an unknown type answers 404.
/// Each pack has a tiny controller of its own that names its key, route and module gate.
/// </summary>
public abstract class PackRecordsControllerBase(ISender mediator) : ApiControllerBase(mediator)
{
    /// <summary>The module key of the pack (<c>PackModule.Key</c>).</summary>
    protected abstract string PackKey { get; }

    [HttpGet("definitions")]
    public Task<PackView> Definitions(CancellationToken ct)
        => Mediator.Send(new GetPackViewQuery(PackKey), ct);

    [HttpGet("summary")]
    public Task<IReadOnlyList<RecordTypeSummary>> Summary(Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetRecordSummaryQuery(PackKey, legalEntityId), ct);

    [HttpGet("records/{type}")]
    public Task<PagedResult<RecordDto>> List(string type, [FromQuery] PagedQuery query, CancellationToken ct)
        => Mediator.Send(new ListRecordsQuery(PackKey, Known(type), query), ct);

    [HttpGet("records/{type}/{id:guid}")]
    public Task<RecordDto> GetById(string type, Guid id, CancellationToken ct)
        => Mediator.Send(new GetRecordCommand(PackKey, Known(type), id), ct);

    [HttpPost("records/{type}")]
    public async Task<IActionResult> Create(string type, RecordInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateRecordCommand(PackKey, Known(type), input), ct);
        return Created($"/api/v1/m/{PackKey}/records/{type}/{created.Id}", created);
    }

    [HttpPut("records/{type}/{id:guid}")]
    public Task<RecordDto> Update(string type, Guid id, RecordInput input, CancellationToken ct)
        => Mediator.Send(new UpdateRecordCommand(PackKey, Known(type), id, input), ct);

    [HttpDelete("records/{type}/{id:guid}")]
    public async Task<IActionResult> Delete(string type, Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteRecordCommand(PackKey, Known(type), id), ct);
        return NoContent();
    }

    [HttpPost("records/{type}/{id:guid}/transition")]
    public Task<RecordDto> Transition(string type, Guid id, RecordTransitionRequest body, CancellationToken ct)
        => Mediator.Send(new TransitionRecordCommand(PackKey, Known(type), id, body.To, body.Reason), ct);

    [HttpPost("records/{type}/{id:guid}/invoice")]
    public Task<RecordDto> Invoice(string type, Guid id, RecordBillingRequest body, CancellationToken ct)
        => Mediator.Send(new InvoiceRecordCommand(PackKey, Known(type), id, body.Date, body.TaxCodeId, body.LineIds), ct);

    [HttpPost("records/{type}/{id:guid}/bill")]
    public Task<RecordDto> Bill(string type, Guid id, RecordBillingRequest body, CancellationToken ct)
        => Mediator.Send(new BillRecordCommand(PackKey, Known(type), id, body.Date, body.TaxCodeId, body.VendorReference), ct);

    [HttpPost("records/{type}/{id:guid}/consume")]
    public Task<RecordDto> Consume(string type, Guid id, RecordConsumeRequest body, CancellationToken ct)
        => Mediator.Send(new ConsumeRecordCommand(PackKey, Known(type), id, body.Date, body.WarehouseId), ct);

    [HttpPost("records/{type}/{id:guid}/progress")]
    public Task<RecordDto> Progress(string type, Guid id, RecordProgressRequest body, CancellationToken ct)
        => Mediator.Send(new UpdateRecordProgressCommand(PackKey, Known(type), id, body.Lines), ct);

    /// <summary>The type as declared, or 404 before anything else runs (the permission check needs a real type to name).</summary>
    private string Known(string type) => PackCatalog.Require(PackKey, type).Key;

    public sealed record RecordTransitionRequest(string To, string? Reason);
    public sealed record RecordBillingRequest(DateOnly? Date, Guid? TaxCodeId, IReadOnlyList<Guid>? LineIds, string? VendorReference);
    public sealed record RecordConsumeRequest(DateOnly? Date, Guid? WarehouseId);
    public sealed record RecordProgressRequest(IReadOnlyList<RecordProgressLine> Lines);
}
