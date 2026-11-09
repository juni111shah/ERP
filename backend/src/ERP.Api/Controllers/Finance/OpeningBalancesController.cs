using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>The opening entry of a legal entity at go-live.</summary>
[Route("api/v1/finance/opening-balances")]
public sealed class OpeningBalancesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpPost]
    [RequirePermission(CorePermissions.JournalsPost)]
    public async Task<IActionResult> Post(OpeningBalanceInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new PostOpeningBalanceCommand(input), ct);
        return Created($"/api/v1/finance/journals/{created.JournalId}", created);
    }

    [HttpPost("{journalId:guid}/reverse")]
    [RequirePermission(CorePermissions.JournalsReverse)]
    public Task<OpeningBalanceDto> Reverse(Guid journalId, ReverseOpeningRequest body, CancellationToken ct)
        => Mediator.Send(new ReverseOpeningBalanceCommand(journalId, body.Reason, body.Date), ct);

    /// <summary>The reason (required) and, optionally, the date of the reversal (the entry's own date otherwise).</summary>
    public sealed record ReverseOpeningRequest(string? Reason, DateOnly? Date = null);
}
