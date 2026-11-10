using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

internal sealed class CreateAccountHandler(IAccountRepository accounts, AccountEditing editing, AccountNumbering numbering, ICustomFieldService fields)
    : ICommandHandler<CreateAccountCommand, AccountDto>
{
    public async Task<AccountDto> Handle(CreateAccountCommand cmd, CancellationToken ct)
    {
        // 1. A free code, a valid parent and a code that follows the numbering mask.
        var i = cmd.Input;
        var code = i.Code.Trim();
        if (await accounts.AnyAsync(a => a.Code == code, ct)) throw new ConflictException($"An account with code '{code}' exists.");
        await editing.EnsureParentAsync(i.ParentId, null, ct);
        await numbering.ValidateAsync(code, i.ParentId, i.Type, ct);

        // 2. Apply the input and the custom fields.
        var account = new Account { Code = code, Type = i.Type };
        AccountEditing.Apply(i, account);
        await fields.ApplyAsync(AccountEditing.CustomFieldType, account, i.CustomFields, isCreate: true, ct);

        // 3. Save; a code taken in the meantime is a conflict.
        accounts.Add(account);
        try { await accounts.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException($"An account with code '{code}' was just created by someone else. Request a new code and try again."); }
        return AccountDto.From(account, await fields.ReadAsync(AccountEditing.CustomFieldType, account, ct));
    }
}
