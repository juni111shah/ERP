using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for credit notes applied to invoices. Implemented in ERP.Infrastructure/Repositories/Finance/CreditApplicationRepository.cs.</summary>
public interface ICreditApplicationRepository : IRepository<CreditApplication>
{
    /// <summary>Every credit application dated on or before <paramref name="asOf"/> (all legal entities when <paramref name="entityId"/> is null).</summary>
    Task<List<CreditApplicationAmount>> ListAsOfAsync(DateOnly asOf, Guid? entityId, CancellationToken ct);
}
