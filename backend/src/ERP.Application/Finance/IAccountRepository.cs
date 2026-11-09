using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for the chart of accounts. Implemented in ERP.Infrastructure/Repositories/Finance/AccountRepository.cs.</summary>
public interface IAccountRepository : IRepository<Account>
{
    /// <summary>The code of every account.</summary>
    Task<List<string>> ListCodesAsync(CancellationToken ct);

    /// <summary>The system roles held by accounts (each account that has one).</summary>
    Task<List<string>> ListSystemRolesAsync(CancellationToken ct);
}
