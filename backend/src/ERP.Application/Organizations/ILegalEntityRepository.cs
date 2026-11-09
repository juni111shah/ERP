using ERP.Application.Abstractions;
using ERP.Domain.Organizations;

namespace ERP.Application.Organizations;

/// <summary>Data access for legal entities. Implemented in ERP.Infrastructure/Repositories/Organizations/LegalEntityRepository.cs.</summary>
public interface ILegalEntityRepository : IRepository<LegalEntity>
{
    /// <summary>The distinct base currencies of one legal entity or (null) all of them.</summary>
    Task<List<string>> ListBaseCurrenciesAsync(Guid? legalEntityId, CancellationToken ct);
}
