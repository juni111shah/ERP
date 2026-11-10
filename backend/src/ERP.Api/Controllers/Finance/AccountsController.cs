using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>The chart of accounts: list, create, change and the next free code.</summary>
[Route("api/v1/finance/accounts")]
public sealed class AccountsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.AccountsRead)]
    public Task<PagedResult<AccountDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList<Account, AccountDto>(query, "account", AccountDto.From, CorePermissions.AccountsRead, ct);

    [HttpPost]
    [RequirePermission(CorePermissions.AccountsWrite)]
    public async Task<IActionResult> Create(AccountInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateAccountCommand(input), ct);
        return Created($"/api/v1/finance/accounts/{created.Id}", created);
    }

    [HttpGet("next-code")]
    [RequirePermission(CorePermissions.AccountsWrite)]
    public Task<NextAccountCodeDto> NextCode(Guid? parentId, string? type, CancellationToken ct)
        => Mediator.Send(new GetNextAccountCodeQuery(parentId, type), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(CorePermissions.AccountsRead)]
    public Task<AccountDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetAccountQuery(id), ct);

    [HttpPut("{id:guid}")]
    [RequirePermission(CorePermissions.AccountsWrite)]
    public Task<AccountDto> Update(Guid id, AccountInput input, CancellationToken ct)
        => Mediator.Send(new UpdateAccountCommand(id, input), ct);
}
