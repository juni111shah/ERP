using ERP.Application.Abstractions;
using ERP.Modules.Sales.Domain;

namespace ERP.Modules.Sales.Repositories;

/// <summary>Data access for sales returns. Implemented in SalesReturnRepository.cs.</summary>
public interface ISalesReturnRepository : IRepository<SalesReturn>
{
    /// <summary>The return with its lines, tracked for changes. Throws NotFoundException.</summary>
    Task<SalesReturn> GetWithLinesForUpdateAsync(Guid id, CancellationToken ct);
}
