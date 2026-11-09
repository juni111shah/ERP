using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for leave types.</summary>
public interface ILeaveTypeRepository : IRepository<LeaveType>
{
    /// <summary>The active leave types whose unused balance is paid out on leaving.</summary>
    Task<List<LeaveType>> ListActiveEncashableAsync(CancellationToken ct);

    /// <summary>The active leave types, by code.</summary>
    Task<List<LeaveType>> ListActiveByCodeAsync(CancellationToken ct);

    /// <summary>The code of every leave type (compared ignoring case).</summary>
    Task<HashSet<string>> CodesAsync(CancellationToken ct);

    /// <summary>The name of every leave type, by id.</summary>
    Task<Dictionary<Guid, string>> NamesByIdAsync(CancellationToken ct);
}
