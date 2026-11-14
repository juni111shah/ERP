using ERP.Api.Infrastructure;
using ERP.Application.Common;
using ERP.Application.Platform;
using ERP.Modularity.Features;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.PlatformAdmin;

/// <summary>The clients of the platform: their plans, states, invoices, users and modules (platform console).</summary>
[Route("api/v1/platform-admin/clients")]
[Authorize(Policy = PlatformAdminRequirement.Policy)]
public sealed class ClientsController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string ModuleToggle = "{orgId:guid}/modules/{key}/{action:regex(^(enable|disable)$)}";

    [HttpGet]
    public Task<PagedResult<ClientRow>> List([FromQuery] ListClientsQuery query, CancellationToken ct) => Mediator.Send(query, ct);

    [PlatformTeamAction("Adding a client is day-to-day work; a manager can suspend or cancel it.")]
    [HttpPost]
    public Task<CreatedClient> Create(CreateClientInput input, CancellationToken ct) => Mediator.Send(new CreateClientCommand(input), ct);

    [HttpGet("{orgId:guid}")]
    public Task<ClientDetail> Get(Guid orgId, CancellationToken ct) => Mediator.Send(new GetClientQuery(orgId), ct);

    [PlatformManager]
    [HttpPut("{orgId:guid}/plan")]
    public Task<ClientDetail> SetPlan(Guid orgId, SetClientPlanInput input, CancellationToken ct) => Mediator.Send(new SetClientPlanCommand(orgId, input), ct);

    [PlatformTeamAction("Notes and contact details only.")]
    [HttpPut("{orgId:guid}/details")]
    public Task<ClientDetail> UpdateDetails(Guid orgId, ClientNotesInput input, CancellationToken ct) => Mediator.Send(new UpdateClientNotesCommand(orgId, input), ct);

    [PlatformTeamAction("Giving a trial more days only widens access.")]
    [HttpPost("{orgId:guid}/extend-trial")]
    public Task<ClientDetail> ExtendTrial(Guid orgId, ExtendTrialInput input, CancellationToken ct) => Mediator.Send(new ExtendClientTrialCommand(orgId, input.Days), ct);

    [PlatformManager]
    [HttpPost("{orgId:guid}/activate")]
    public Task<ClientDetail> Activate(Guid orgId, ActivateInput input, CancellationToken ct) => Mediator.Send(new ActivateClientCommand(orgId, input.IssueInvoice), ct);

    [PlatformManager]
    [HttpPost("{orgId:guid}/suspend")]
    public Task<ClientDetail> Suspend(Guid orgId, ReasonInput input, CancellationToken ct) => Mediator.Send(new SuspendClientCommand(orgId, input.Reason), ct);

    [PlatformManager]
    [HttpPost("{orgId:guid}/reactivate")]
    public Task<ClientDetail> Reactivate(Guid orgId, CancellationToken ct) => Mediator.Send(new ReactivateClientCommand(orgId), ct);

    [PlatformManager]
    [HttpPost("{orgId:guid}/cancel")]
    public Task<ClientDetail> Cancel(Guid orgId, ReasonInput input, CancellationToken ct) => Mediator.Send(new CancelClientCommand(orgId, input.Reason), ct);

    [PlatformTeamAction("Answers a client's request; the plan stays as it is.")]
    [HttpPost("{orgId:guid}/decline-plan-request")]
    public Task<ClientDetail> DeclinePlanRequest(Guid orgId, CancellationToken ct) => Mediator.Send(new DeclinePlanRequestCommand(orgId), ct);

    [PlatformTeamAction("Billing work; a wrong invoice is voided by a manager.")]
    [HttpPost("{orgId:guid}/invoices")]
    public Task<InvoiceRow> IssueInvoice(Guid orgId, IssueInvoiceInput input, CancellationToken ct) => Mediator.Send(new IssueClientInvoiceCommand(orgId, input), ct);

    [PlatformManager]
    [HttpPost("{orgId:guid}/users/{userId:guid}/temporary-password")]
    public Task<TemporaryPassword> ResetUserPassword(Guid orgId, Guid userId, CancellationToken ct) => Mediator.Send(new ResetClientUserPasswordCommand(orgId, userId), ct);

    [PlatformManager]
    [HttpPut("{orgId:guid}/demo")]
    public Task<bool> SetDemo(Guid orgId, SetClientDemoInput input, CancellationToken ct) => Mediator.Send(new SetClientDemoCommand(orgId, input.IsDemo), ct);

    // {action} is the action name in MVC, so the one route is served by one action per value.
    [PlatformManager]
    [HttpPost(ModuleToggle)]
    [ActionName("enable")]
    public Task<ModuleState> EnableModule(Guid orgId, string key, CancellationToken ct) => Mediator.Send(new SetClientModuleCommand(orgId, key, true), ct);

    [PlatformManager]
    [HttpPost(ModuleToggle)]
    [ActionName("disable")]
    public Task<ModuleState> DisableModule(Guid orgId, string key, CancellationToken ct) => Mediator.Send(new SetClientModuleCommand(orgId, key, false), ct);
}
