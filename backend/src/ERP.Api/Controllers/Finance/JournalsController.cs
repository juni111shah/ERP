using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>Manual journals: drafts, posting and reversal.</summary>
[Route("api/v1/finance/journals")]
public sealed class JournalsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.JournalsRead)]
    public Task<PagedResult<JournalSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, JournalSummaryDto.Projection, CorePermissions.JournalsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(CorePermissions.JournalsRead)]
    public Task<JournalDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetJournalQuery(id), ct);

    [HttpPost]
    [RequirePermission(CorePermissions.JournalsWrite)]
    public async Task<IActionResult> Create(JournalInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateJournalCommand(input), ct);
        return Created($"/api/v1/finance/journals/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(CorePermissions.JournalsWrite)]
    public Task<JournalDto> Update(Guid id, JournalInput input, CancellationToken ct)
        => Mediator.Send(new UpdateJournalCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(CorePermissions.JournalsWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteJournalCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/post")]
    [RequirePermission(CorePermissions.JournalsPost)]
    public Task<JournalDto> Post(Guid id, CancellationToken ct)
        => Mediator.Send(new PostJournalCommand(id), ct);

    [HttpPost("{id:guid}/reverse")]
    [RequirePermission(CorePermissions.JournalsReverse)]
    public Task<JournalDto> Reverse(Guid id, ReverseRequest? body, CancellationToken ct)
        => Mediator.Send(new ReverseJournalCommand(id, body?.Date, body?.Memo ?? body?.Reason), ct);

    /// <summary>The journal-detail screen sends its askReason text as <c>memo</c>; <c>reason</c> is accepted as an alias (same field as the void endpoints).</summary>
    public sealed record ReverseRequest(DateOnly? Date, string? Memo, string? Reason = null);
}
