using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for the chart of accounts. Implemented in ERP.Infrastructure/Repositories/Finance/AccountRepository.cs.</summary>
public interface IAccountRepository : IRepository<Account>
{
    /// <summary>Code and name by account id, for the ids that exist.</summary>
    Task<Dictionary<Guid, AccountLabel>> GetLabelsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>How every account is classified (type, system role, bank flag, reporting group).</summary>
    Task<List<AccountClassification>> ListClassificationsAsync(CancellationToken ct);
}

public sealed record AccountLabel(Guid Id, string Code, string Name);

public sealed record AccountClassification(Guid Id, AccountType Type, string? SystemRole, bool IsBank, string? ReportingGroup);
