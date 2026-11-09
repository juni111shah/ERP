using ERP.Modularity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.IndustryPacks.Controllers;

/// <summary>The records of the Expenses pack (the shared pack routes, see <see cref="PackRecordsControllerBase"/>).</summary>
[ModuleGate("expenses")]
[Route("api/v1/m/expenses")]
[Authorize]
public sealed class ExpensesRecordsController(ISender mediator) : PackRecordsControllerBase(mediator)
{
    protected override string PackKey => "expenses";
}