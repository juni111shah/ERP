using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>Currencies.</summary>
[Route("api/v1/finance/currencies")]
public sealed class CurrenciesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.CurrenciesRead)]
    public Task<PagedResult<CurrencyDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, CurrencyDto.Projection, CorePermissions.CurrenciesRead, ct);
}
