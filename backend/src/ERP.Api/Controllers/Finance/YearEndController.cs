using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>Fiscal year closes: the list, closing a year and reopening it.</summary>
[Route("api/v1/finance/year-end")]
public sealed class YearEndController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.ReportsRead)]
    public Task<PagedResult<FiscalYearCloseDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, FiscalYearCloseDto.Projection, CorePermissions.ReportsRead, ct);

    [HttpPost("close")]
    [RequirePermission(CorePermissions.YearEndClose)]
    public Task<FiscalYearCloseDto> Close(CloseFiscalYearCommand command, CancellationToken ct)
        => Mediator.Send(command, ct);

    [HttpPost("reopen")]
    [RequirePermission(CorePermissions.YearEndReopen)]
    public Task<FiscalYearCloseDto> Reopen(ReopenFiscalYearCommand command, CancellationToken ct)
        => Mediator.Send(command, ct);
}
