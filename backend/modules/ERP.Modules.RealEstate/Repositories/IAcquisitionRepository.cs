using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for property acquisitions. Implemented in AcquisitionRepository.cs.</summary>
public interface IAcquisitionRepository : IRepository<Acquisition>
{
    /// <summary>Price and down payment of every acquisition that is not cancelled.</summary>
    Task<List<AcquisitionAmounts>> ListLiveAmountsAsync(CancellationToken ct);
}

/// <summary>An acquisition's price and down payment.</summary>
public sealed record AcquisitionAmounts(Guid Id, decimal PurchasePrice, decimal DownPayment);
