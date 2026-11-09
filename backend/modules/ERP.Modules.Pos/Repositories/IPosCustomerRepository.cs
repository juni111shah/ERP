using ERP.Application.Abstractions;
using ERP.Domain.Parties;
using ERP.Modules.Pos.Features;

namespace ERP.Modules.Pos.Repositories;

/// <summary>The customers as the till looks them up. Implemented in PosCustomerRepository.cs.</summary>
public interface IPosCustomerRepository : IRepository<Party>
{
    /// <summary>Up to <paramref name="limit"/> customers (or parties with no role) by name, matching part of the name, code, phone or tax number.</summary>
    Task<List<PosCustomerDto>> SearchAsync(string? search, int limit, CancellationToken ct);
}
