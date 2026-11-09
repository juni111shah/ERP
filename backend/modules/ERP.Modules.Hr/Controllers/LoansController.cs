using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Staff loans and salary advances.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/loans")]
public sealed class LoansController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    [RequirePermission(P.PayrollRead)]
    public Task<PagedResult<LoanRow>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, LoanRow.Projection, P.PayrollRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.PayrollRead)]
    public Task<LoanDto> Get(Guid id, CancellationToken ct) => Mediator.Send(new GetLoanQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.PayrollManage)]
    public async Task<IActionResult> Create(LoanInput input, CancellationToken ct)
    {
        var l = await Mediator.Send(new CreateLoanCommand(input), ct);
        return Created($"{Base}/loans/{l.Id}", l);
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(P.PayrollManage)]
    public Task<LoanDto> Cancel(Guid id, CancellationToken ct) => Mediator.Send(new CancelLoanCommand(id), ct);

    [HttpPost("{id:guid}/disburse")]
    [RequirePermission(P.PayrollPay)]
    public Task<LoanDto> Disburse(Guid id, LoanDisburseRequest b, CancellationToken ct) => Mediator.Send(new DisburseLoanCommand(id, b.BankAccountId, b.Date), ct);

    /// <summary>The bank account the loan is paid from, and the date.</summary>
    public sealed record LoanDisburseRequest(Guid BankAccountId, DateOnly Date);
}
