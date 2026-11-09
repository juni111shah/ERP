using ERP.Modularity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.IndustryPacks.Controllers;

/// <summary>The records of the Trade pack (the shared pack routes, see <see cref="PackRecordsControllerBase"/>).</summary>
[ModuleGate("trade")]
[Route("api/v1/m/trade")]
[Authorize]
public sealed class TradeRecordsController(ISender mediator) : PackRecordsControllerBase(mediator)
{
    protected override string PackKey => "trade";
}