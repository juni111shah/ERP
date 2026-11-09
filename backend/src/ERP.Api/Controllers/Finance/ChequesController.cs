using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>The cheque / post-dated cheque register (cheques received from customers).</summary>
[CapabilityGate("cheques")]
[Route("api/v1/finance/cheques")]
public sealed class ChequesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.ChequesRead)]
    public Task<PagedResult<ChequeSummaryRow>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, ChequeSummaryRow.Projection, CorePermissions.ChequesRead, ct);

    [HttpGet("summary")]
    [RequirePermission(CorePermissions.ChequesRead)]
    public Task<ChequeSummaryDto> Summary(DateOnly? asOf, CancellationToken ct)
        => Mediator.Send(new GetChequeSummaryQuery(asOf ?? DateOnly.FromDateTime(DateTime.UtcNow)), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(CorePermissions.ChequesRead)]
    public Task<ChequeDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetChequeByIdQuery(id), ct);

    [HttpPost]
    [RequirePermission(CorePermissions.ChequesWrite)]
    public async Task<IActionResult> Register(ChequeInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new RegisterChequeCommand(input), ct);
        return Created($"/api/v1/finance/cheques/{created.Id}", created);
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(CorePermissions.ChequesWrite)]
    public Task<ChequeDto> Cancel(Guid id, CancellationToken ct)
        => Mediator.Send(new CancelChequeCommand(id), ct);

    [HttpPost("{id:guid}/deposit")]
    [RequirePermission(CorePermissions.ChequesApprove)]
    public Task<ChequeDto> Deposit(Guid id, DepositRequest body, CancellationToken ct)
        => Mediator.Send(new DepositChequeCommand(id, body.BankAccountId, body.Date), ct);

    [HttpPost("{id:guid}/clear")]
    [RequirePermission(CorePermissions.ChequesApprove)]
    public Task<ChequeDto> Clear(Guid id, ClearChequeRequest body, CancellationToken ct)
        => Mediator.Send(new ClearChequeCommand(id, body.Date), ct);

    [HttpPost("{id:guid}/bounce")]
    [RequirePermission(CorePermissions.ChequesApprove)]
    public Task<ChequeDto> Bounce(Guid id, BounceRequest body, CancellationToken ct)
        => Mediator.Send(new BounceChequeCommand(id, body.Date, body.Reason), ct);

    [HttpPost("{id:guid}/follow-up")]
    [RequirePermission(CorePermissions.ChequesWrite)]
    public Task<ChequeDto> FollowUp(Guid id, FollowUpRequest body, CancellationToken ct)
        => Mediator.Send(new LogChequeFollowUpCommand(id, body.Date, body.Note), ct);

    [HttpPost("{id:guid}/replace")]
    [RequirePermission(CorePermissions.ChequesApprove)]
    public Task<ChequeDto> Replace(Guid id, ChequeInput replacement, CancellationToken ct)
        => Mediator.Send(new ReplaceChequeCommand(id, replacement), ct);

    /// <summary>The bank account the cheque goes into and the deposit date.</summary>
    public sealed record DepositRequest(Guid BankAccountId, DateOnly Date);

    /// <summary>The date the bank returned the cheque and why.</summary>
    public sealed record BounceRequest(DateOnly Date, string Reason);

    /// <summary>What was done to chase the customer, and when (today by default).</summary>
    public sealed record FollowUpRequest(string Note, DateOnly? Date = null);
}
