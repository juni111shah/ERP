using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>
/// Step-by-step chart of accounts: one level per request (types -> sub-categories -> primary accounts -> children), each with hasChildren.
/// </summary>
[Route("api/v1/finance/accounts")]
[RequirePermission(CorePermissions.AccountsRead)]
public sealed class AccountTreeController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("tree/categories")]
    public Task<IReadOnlyList<AccountCategoryDto>> Categories([FromQuery] GetAccountCategoriesQuery query, CancellationToken ct)
        => Mediator.Send(query, ct);

    [HttpGet("tree/categories/{type}/accounts")]
    public Task<PagedResult<AccountNodeDto>> CategoryAccounts(string type, [FromQuery] ListCategoryAccountsQuery query, CancellationToken ct)
        => Mediator.Send(query with { Type = type }, ct);

    [HttpGet("tree/categories/{type}/subcategories")]
    public Task<IReadOnlyList<AccountSubcategoryDto>> Subcategories(string type, [FromQuery] GetAccountSubcategoriesQuery query, CancellationToken ct)
        => Mediator.Send(query with { Type = type }, ct);

    [HttpGet("tree/categories/{type}/subcategories/{key}/accounts")]
    public Task<PagedResult<AccountNodeDto>> SubcategoryAccounts(string type, string key, [FromQuery] ListSubcategoryAccountsQuery query, CancellationToken ct)
        => Mediator.Send(query with { Type = type, Key = key }, ct);

    [HttpGet("{id:guid}/children")]
    public Task<PagedResult<AccountNodeDto>> Children(Guid id, [FromQuery] ListAccountChildrenQuery query, CancellationToken ct)
        => Mediator.Send(query with { Id = id }, ct);

    [HttpGet("{id:guid}/path")]
    public Task<IReadOnlyList<AccountNodeDto>> Path(Guid id, CancellationToken ct)
        => Mediator.Send(new GetAccountPathQuery(id), ct);
}
