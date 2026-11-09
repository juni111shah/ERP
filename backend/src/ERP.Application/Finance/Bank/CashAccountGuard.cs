using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Checks that an account is an active bank or cash account; shared by statement import and transfers.</summary>
public sealed class CashAccountGuard(IReadRepository<Account> accounts)
{
    /// <summary>Throws a field error on <paramref name="field"/> when the account is not an active bank or cash account.</summary>
    public async Task RequireAsync(Guid id, string field, CancellationToken ct)
    {
        var a = await accounts.FindAsync(id, ct);
        if (a is null || !BankAccountRules.IsCashOrBank(a)) throw new FieldValidationException(field, "Choose an active bank or cash account.");
    }
}
