using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Security;

/// <summary>The audit trail: the log, one event, a record's history and the filters.</summary>
[Route("api/v1/audit")]
public sealed class AuditController(ISender mediator) : ApiControllerBase(mediator)
{
    // ?filter=recordType:eq:Party&filter=recordId:eq:{id}&sort=-occurredAt
    [HttpGet("record")]
    [RequirePermission(CorePermissions.AuditRead)]
    public Task<PagedResult<HistoryEntry>> GetRecordHistory(string recordType, string recordId, int? page, int? pageSize, CancellationToken ct)
        => Mediator.Send(new GetRecordHistoryQuery(recordType, recordId, page ?? 1, pageSize ?? 25), ct);

    [HttpGet("facets")]
    [RequirePermission(CorePermissions.AuditRead)]
    public Task<AuditFacets> GetFacets(CancellationToken ct) => Mediator.Send(new GetAuditFacetsQuery(), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(CorePermissions.AuditRead)]
    public Task<AuditEventDetail> Get(Guid id, CancellationToken ct) => Mediator.Send(new GetAuditEventQuery(id), ct);

    [HttpGet]
    [RequirePermission(CorePermissions.AuditRead)]
    public Task<PagedResult<AuditRow>> List([FromQuery] ListAuditEventsQuery query, CancellationToken ct) => Mediator.Send(query, ct);
}
