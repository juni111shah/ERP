using ERP.Application.Abstractions;
using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Repositories;

/// <summary>Data access for cash registers. Implemented in PosRegisterRepository.cs.</summary>
public interface IPosRegisterRepository : IRepository<PosRegister>
{
    /// <summary>The active registers, by name.</summary>
    Task<List<PosRegister>> ListActiveByNameAsync(CancellationToken ct);

    /// <summary>The name of every register, by id.</summary>
    Task<Dictionary<Guid, string>> NamesAsync(CancellationToken ct);
}
