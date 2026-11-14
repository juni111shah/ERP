using ERP.Application.Search;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Dashboard;

/// <summary>The global search box across every enabled module.</summary>
[Route("api/v1/search")]
public sealed class SearchController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [SignedIn("Each search provider returns hits only for record types the caller may read")]
    public Task<SearchResult> Search(string q, CancellationToken ct)
        => Mediator.Send(new GlobalSearchQuery(q), ct);
}
