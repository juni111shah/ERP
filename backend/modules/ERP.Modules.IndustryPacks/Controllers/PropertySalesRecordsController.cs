using ERP.Modularity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.IndustryPacks.Controllers;

/// <summary>The records of the PropertySales pack (the shared pack routes, see <see cref="PackRecordsControllerBase"/>).</summary>
[ModuleGate("propertysales")]
[Route("api/v1/m/propertysales")]
public sealed class PropertySalesRecordsController(ISender mediator) : PackRecordsControllerBase(mediator)
{
    protected override string PackKey => "propertysales";
}