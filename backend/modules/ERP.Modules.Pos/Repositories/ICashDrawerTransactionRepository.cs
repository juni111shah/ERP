using ERP.Application.Abstractions;
using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Repositories;

/// <summary>Data access for cash drawer movements. Implemented in CashDrawerTransactionRepository.cs.</summary>
public interface ICashDrawerTransactionRepository : IRepository<CashDrawerTransaction>
{
    /// <summary>The drawer movements of a shift, read-only, in no particular order.</summary>
    Task<List<CashDrawerTransaction>> ListForSessionAsync(Guid sessionId, CancellationToken ct);
}
