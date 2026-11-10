using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for financing instalments. Implemented in FundingInstalmentRepository.cs.</summary>
public interface IFundingInstalmentRepository : IRepository<FundingInstalment>
{
    /// <summary>The principal of the paid instalments of the funding sources, added up.</summary>
    Task<decimal> PaidPrincipalOfSourcesAsync(List<Guid> fundingSourceIds, CancellationToken ct);

    /// <summary>The schedule of a funding source, in sequence.</summary>
    Task<List<FundingInstalment>> ListOfSourceAsync(Guid fundingSourceId, CancellationToken ct);
}
