using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

/// <summary>The checks shared by create, update and delete register: a usable warehouse and ledger accounts, and no open shift.</summary>
public sealed class PosRegisterSetup(IRepository<PosSession> sessions, IReadRepository<Account> ledgerAccounts, IStockPostingService stock)
{
    public async Task EnsureNoOpenSessionAsync(Guid registerId, string action, CancellationToken ct)
    {
        if (await sessions.AnyAsync(s => s.RegisterId == registerId && s.Status == PosSessionStatus.Open, ct))
            throw new ConflictException($"Close the open session before you {action} this register.");
    }

    public async Task EnsureSetupAsync(Guid legalEntityId, Guid? warehouseId, Guid? cash, Guid? card, Guid? variance, Guid? expense, CancellationToken ct)
    {
        if (warehouseId is { } w && !await stock.IsWarehouseUsableAsync(w, legalEntityId, ct))
            throw new FieldValidationException("warehouseId", "Choose an active warehouse of this legal entity.");
        await EnsureAccountAsync("defaultCashAccountId", cash, ct);
        await EnsureAccountAsync("cardAccountId", card, ct);
        await EnsureAccountAsync("varianceAccountId", variance, ct);
        await EnsureAccountAsync("expenseAccountId", expense, ct);
    }

    /// <summary>Caught here, not at the first sale: a register's own till account is often a plain asset account, so only "can it be posted to" is asked.</summary>
    private async Task EnsureAccountAsync(string field, Guid? id, CancellationToken ct)
    {
        if (id is null || id == Guid.Empty) return;
        var account = await ledgerAccounts.FindAsync(id.Value, ct);
        if (account is null || !account.IsPostable || !account.IsActive) throw new FieldValidationException(field, "Choose an active account that can be posted to.");
    }

    /// <summary>Left out (null) keeps the current account; an empty GUID clears it.</summary>
    public static Guid? Merge(Guid? incoming, Guid? current) => incoming is null ? current : incoming == Guid.Empty ? null : incoming;
}
