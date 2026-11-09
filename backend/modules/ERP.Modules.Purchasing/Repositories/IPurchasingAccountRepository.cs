using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Modules.Purchasing.Repositories;

/// <summary>The ledger accounts purchasing bills to. Implemented in PurchasingAccountRepository.cs.</summary>
public interface IPurchasingAccountRepository : IRepository<Account>
{
    /// <summary>An active, postable account holding the system role; null when there is none.</summary>
    Task<Account?> FindPostableByRoleAsync(string role, CancellationToken ct);
}
