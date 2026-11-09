using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>The period lock of a legal entity's books.</summary>
[Route("api/v1/legal-entities")]
public sealed class BooksLockController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpPut("{id:guid}/books-lock")]
    [RequirePermission(CorePermissions.EntitiesWrite)]
    public async Task<IActionResult> LockBooks(Guid id, LockBooksRequest body, CancellationToken ct)
    {
        await Mediator.Send(new LockBooksCommand(id, body.Through), ct);
        return NoContent();
    }

    /// <summary>The last day the books are closed for; null lifts the lock.</summary>
    public sealed record LockBooksRequest(DateOnly? Through);
}
