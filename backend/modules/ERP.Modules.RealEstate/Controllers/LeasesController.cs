using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Leases: lifecycle (create, activate, renew, terminate, move out), rent schedule, security deposit and termination credits.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/leases")]
public sealed class LeasesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.LeasesRead)]
    public Task<PagedResult<LeaseListItem>> List([FromQuery] ListLeasesQuery query, CancellationToken ct)
        => Mediator.Send(query, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.LeasesRead)]
    public Task<LeaseDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetLeaseQuery(id), ct);

    [HttpGet("{id:guid}/deposit")]
    [RequirePermission(P.LeasesRead)]
    public Task<DepositDto> GetDeposit(Guid id, CancellationToken ct)
        => Mediator.Send(new GetSecurityDepositQuery(id), ct);

    [HttpPost("{id:guid}/deposit")]
    [RequirePermission(P.DepositsManage)]
    public Task<DepositDto> RecordDeposit(Guid id, DepositRequest b, CancellationToken ct)
        => Mediator.Send(new RecordSecurityDepositCommand(id, b.BankAccountId, b.Date, b.Amount), ct);

    [HttpPost("{id:guid}/deposit/settle")]
    [RequirePermission(P.DepositsManage)]
    public Task<DepositDto> SettleDeposit(Guid id, SettleDepositInput input, CancellationToken ct)
        => Mediator.Send(new SettleSecurityDepositCommand(id, input), ct);

    [HttpPost("{id:guid}/deposit/transfer")]
    [RequirePermission(P.DepositsManage)]
    public Task<DepositDto> TransferDeposit(Guid id, TransferDepositRequest b, CancellationToken ct)
        => Mediator.Send(new TransferSecurityDepositCommand(id, b.ToLeaseId), ct);

    [HttpPost("{id:guid}/termination-credits")]
    [RequirePermission(P.LeasesTerminate)]
    public Task<IReadOnlyList<TerminationCreditDto>> CreateTerminationCredits(Guid id, CancellationToken ct)
        => Mediator.Send(new CreateTerminationCreditsCommand(id), ct);

    [HttpGet("{id:guid}/schedule")]
    [RequirePermission(P.LeasesRead)]
    public async Task<IReadOnlyList<InstalmentDto>> GetSchedule(Guid id, CancellationToken ct)
        => (await Mediator.Send(new GetLeaseQuery(id), ct)).Schedule;

    [HttpPost]
    [Authorize] // leases.create, or leases.activate with ActivateNow: the command declares which
    public async Task<IActionResult> Create(LeaseInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateLeaseCommand(input), ct);
        return Created($"/api/v1/m/realestate/leases/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.LeasesEdit)]
    public Task<LeaseDto> Update(Guid id, LeaseInput input, CancellationToken ct)
        => Mediator.Send(new UpdateLeaseCommand(id, input), ct);

    [HttpPost("{id:guid}/activate")]
    [RequirePermission(P.LeasesActivate)]
    public Task<LeaseDto> Activate(Guid id, CancellationToken ct)
        => Mediator.Send(new ActivateLeaseCommand(id), ct);

    [HttpPost("{id:guid}/renew")]
    [RequirePermission(P.LeasesRenew)]
    public async Task<IActionResult> Renew(Guid id, RenewalInput? input, CancellationToken ct)
    {
        var renewal = await Mediator.Send(new RenewLeaseCommand(id, input ?? new(null, null, null, null, null)), ct);
        return Created($"/api/v1/m/realestate/leases/{renewal.Id}", renewal);
    }

    [HttpPost("{id:guid}/terminate")]
    [RequirePermission(P.LeasesTerminate)]
    public Task<TerminationResultDto> Terminate(Guid id, TerminateRequest body, CancellationToken ct)
        => Mediator.Send(new TerminateLeaseCommand(id, body.Date, body.Reason), ct);

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(P.LeasesEdit)]
    public Task<LeaseDto> Cancel(Guid id, CancellationToken ct)
        => Mediator.Send(new CancelLeaseCommand(id), ct);

    [HttpPost("{id:guid}/holding-over")]
    [RequirePermission(P.LeasesEdit)]
    public Task<LeaseDto> FlagHoldingOver(Guid id, HoldingOverRequest body, CancellationToken ct)
        => Mediator.Send(new FlagHoldingOverCommand(id, body.HoldingOver), ct);

    [HttpPost("{id:guid}/move-out")]
    [RequirePermission(P.LeasesTerminate)]
    public Task<LeaseDto> ConfirmMoveOut(Guid id, CancellationToken ct)
        => Mediator.Send(new ConfirmLeaseMoveOutCommand(id), ct);

    [HttpPost("process-expiries")]
    [RequirePermission(P.LeasesActivate)]
    public Task<ExpiryRunResultDto> ProcessExpiries(CancellationToken ct)
        => Mediator.Send(new ProcessLeaseExpiriesCommand(), ct);

    public sealed record DepositRequest(Guid BankAccountId, DateOnly Date, decimal Amount);
    public sealed record TransferDepositRequest(Guid ToLeaseId);
    public sealed record TerminateRequest(DateOnly Date, string Reason);
    public sealed record HoldingOverRequest(bool HoldingOver);
}
