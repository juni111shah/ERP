using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for employee documents.</summary>
public interface IEmployeeDocumentRepository : IRepository<EmployeeDocument>
{
    /// <summary>The documents with an expiry date on or before <paramref name="limit"/> (already expired ones included).</summary>
    Task<List<EmployeeDocument>> ListExpiringByAsync(DateOnly limit, CancellationToken ct);
}
