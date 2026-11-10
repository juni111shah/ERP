using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>Data access for leads from the public assistant. Implemented in ERP.Infrastructure/Repositories/Platform/BrandLeadRepository.cs.</summary>
public interface IBrandLeadRepository : IRepository<BrandLead>
{
    /// <summary>One page of leads, newest first, optionally in one state and matching a search, with the total count.</summary>
    Task<(List<BrandLead> Rows, int Total)> ListPageAsync(BrandLeadStatus? status, string? search, int page, int size, CancellationToken ct);
}
