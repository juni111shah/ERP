using ERP.Application.Common;
using ERP.Application.Customization;

namespace ERP.Application.Finance;

internal sealed class UpdateAccountHandler(IAccountRepository accounts, AccountEditing editing, AccountNumbering numbering, ICustomFieldService fields)
    : ICommandHandler<UpdateAccountCommand, AccountDto>
{
    public async Task<AccountDto> Handle(UpdateAccountCommand cmd, CancellationToken ct)
    {
        // 1. Load it.
        var i = cmd.Input;
        var account = await accounts.GetForUpdateAsync(cmd.Id, ct);
        var code = i.Code.Trim();
        var (oldCode, oldParent, oldType) = (account.Code, account.ParentId, account.Type);

        // 2. A system account keeps its code, type and active state; any other account may take a free code.
        if (account.IsSystem)
        {
            if (code != account.Code || i.Type != account.Type) throw new ConflictException("The code and type of a system account cannot change (posting rules depend on them).");
            if (!i.IsActive) throw new ConflictException("A system account cannot be deactivated.");
        }
        else
        {
            if (code != account.Code && await accounts.AnyAsync(a => a.Code == code, ct)) throw new ConflictException($"An account with code '{code}' exists.");
            account.Code = code; account.Type = i.Type;
        }

        // 3. A valid parent. Only a changed code, parent or type is checked against the mask, so editing legacy accounts keeps working.
        await editing.EnsureParentAsync(i.ParentId, account.Id, ct);
        if (code != oldCode || i.ParentId != oldParent || i.Type != oldType) await numbering.ValidateAsync(code, i.ParentId, i.Type, ct);

        // 4. Apply the input and the custom fields, and save.
        AccountEditing.Apply(i, account);
        await fields.ApplyAsync(AccountEditing.CustomFieldType, account, i.CustomFields, isCreate: false, ct);
        await accounts.SaveChangesAsync(ct);
        return AccountDto.From(account, await fields.ReadAsync(AccountEditing.CustomFieldType, account, ct));
    }
}
