using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for the funding sources of acquisitions. Implemented in FundingSourceRepository.cs.</summary>
public interface IFundingSourceRepository : IRepository<FundingSource>
{
    /// <summary>The principal of each funding source of the acquisitions.</summary>
    Task<List<FundingPrincipal>> ListPrincipalsOfAcquisitionsAsync(List<Guid> acquisitionIds, CancellationToken ct);

    /// <summary>Every funding source, earliest first due, then the largest.</summary>
    Task<List<FundingSource>> ListAllByDueAsync(CancellationToken ct);

    /// <summary>The funding sources of an acquisition, tracked for changes.</summary>
    Task<List<FundingSource>> ListOfAcquisitionForUpdateAsync(Guid acquisitionId, CancellationToken ct);

    /// <summary>The funding sources of an acquisition, earliest first due, then the largest, tracked for changes.</summary>
    Task<List<FundingSource>> ListOfAcquisitionByDueForUpdateAsync(Guid acquisitionId, CancellationToken ct);

    /// <summary>The funding sources of an acquisition, earliest first due, then the largest.</summary>
    Task<List<FundingSource>> ListOfAcquisitionAsync(Guid acquisitionId, CancellationToken ct);
}

/// <summary>A funding source's principal.</summary>
public sealed record FundingPrincipal(Guid Id, decimal Principal);
