using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for the security deposit register. Implemented in DepositTransactionRepository.cs.</summary>
public interface IDepositTransactionRepository : IRepository<DepositTransaction>
{
    /// <summary>The deposit movements of a lease, oldest first.</summary>
    Task<List<DepositTransaction>> ListOfLeaseAsync(Guid leaseId, CancellationToken ct);
}
