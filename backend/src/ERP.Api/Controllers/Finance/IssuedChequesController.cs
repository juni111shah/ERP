using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>Cheques issued with a vendor payment. Not behind the "cheques" capability, as payments are not.</summary>
[Route("api/v1/finance/cheques")]
public sealed class IssuedChequesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpPost("{id:guid}/clear-issued")]
    [RequirePermission(CorePermissions.ChequesApprove)]
    public Task<PaymentDto> ClearIssued(Guid id, ClearChequeRequest body, CancellationToken ct)
        => Mediator.Send(new ClearIssuedChequeCommand(id, body.Date), ct);
}
