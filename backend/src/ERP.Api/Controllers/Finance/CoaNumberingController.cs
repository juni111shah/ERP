using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>The chart-of-accounts numbering mask.</summary>
[Route("api/v1/finance/coa-numbering")]
public sealed class CoaNumberingController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.AccountsRead)]
    public Task<CoaNumberingDto> Get(CancellationToken ct)
        => Mediator.Send(new GetCoaNumberingQuery(), ct);

    [HttpPut]
    [RequirePermission(CorePermissions.SettingsWrite)]
    public Task<CoaNumberingDto> Set(CoaNumberingRequest body, CancellationToken ct)
        => Mediator.Send(new SetCoaNumberingCommand(body.Mode, body.Segments, body.Separator), ct);

    /// <summary>The mode (free or hierarchical), the digits per level and the separator between levels.</summary>
    public sealed record CoaNumberingRequest(string Mode, IReadOnlyList<int> Segments, string Separator);
}
