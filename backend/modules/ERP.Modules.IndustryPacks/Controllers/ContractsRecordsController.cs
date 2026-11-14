using ERP.Modularity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.IndustryPacks.Controllers;

/// <summary>The records of the Contracts pack (the shared pack routes, see <see cref="PackRecordsControllerBase"/>).</summary>
[ModuleGate("contracts")]
[Route("api/v1/m/contracts")]
public sealed class ContractsRecordsController(ISender mediator) : PackRecordsControllerBase(mediator)
{
    protected override string PackKey => "contracts";
}