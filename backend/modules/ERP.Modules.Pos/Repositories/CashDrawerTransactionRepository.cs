using ERP.Application.Abstractions;
using ERP.Modules.Pos.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Repositories;

internal sealed class CashDrawerTransactionRepository(IRepository<CashDrawerTransaction> inner) : RepositoryBase<CashDrawerTransaction>(inner), ICashDrawerTransactionRepository
{
    public Task<List<CashDrawerTransaction>> ListForSessionAsync(Guid sessionId, CancellationToken ct)
        => Query(new Specification<CashDrawerTransaction>(t => t.SessionId == sessionId)).ToListAsync(ct);
}
