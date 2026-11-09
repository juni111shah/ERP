using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for the chart of accounts. Implemented in ERP.Infrastructure/Repositories/Finance/AccountRepository.cs.</summary>
public interface IAccountRepository : IRepository<Account>
{
    /// <summary>Those of the accounts that are active and postable, with their system role.</summary>
    Task<List<AccountRole>> ListPostableRolesAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The active bank and cash accounts (asset accounts flagged as bank, or with the "cash" or "bank" role), by code.</summary>
    Task<List<CashAccount>> ListCashAndBankAsync(CancellationToken ct);
}

/// <summary>An account and its system role.</summary>
public sealed record AccountRole(Guid Id, string? SystemRole);

/// <summary>A bank or cash account.</summary>
public sealed record CashAccount(Guid Id, string Code, string Name, bool IsBank);
