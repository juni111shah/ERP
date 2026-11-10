using ERP.Application.Common;
using ERP.Application.Customization;

namespace ERP.Application.Finance;

internal sealed class GetAccountHandler(IAccountRepository accounts, ICustomFieldService fields) : IQueryHandler<GetAccountQuery, AccountDto>
{
    public async Task<AccountDto> Handle(GetAccountQuery q, CancellationToken ct)
    {
        var account = await accounts.GetAsync(q.Id, ct);
        return AccountDto.From(account, await fields.ReadAsync(AccountEditing.CustomFieldType, account, ct));
    }
}
