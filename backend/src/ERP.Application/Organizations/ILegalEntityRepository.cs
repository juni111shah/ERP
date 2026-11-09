using ERP.Application.Abstractions;
using ERP.Domain.Organizations;

namespace ERP.Application.Organizations;

/// <summary>Data access for legal entities. Implemented in ERP.Infrastructure/Repositories/Organizations/LegalEntityRepository.cs.</summary>
public interface ILegalEntityRepository : IRepository<LegalEntity>
{
    /// <summary>The distinct base currencies of one legal entity or (null) all of them.</summary>
    Task<List<string>> ListBaseCurrenciesAsync(Guid? legalEntityId, CancellationToken ct);

    /// <summary>The legal entities in <paramref name="allowed"/> (all of them when null), by code.</summary>
    Task<List<LegalEntity>> ListAllowedAsync(IReadOnlyList<Guid>? allowed, CancellationToken ct);

    /// <summary>Which of <paramref name="ids"/> are legal entities of this workspace.</summary>
    Task<List<Guid>> ListExistingIdsAsync(Guid[] ids, CancellationToken ct);

    /// <summary>Name and base currency of each of the legal entities that exists, by id.</summary>
    Task<Dictionary<Guid, LegalEntityNameAndCurrency>> MapNamesAndCurrenciesAsync(List<Guid> ids, CancellationToken ct);
}

/// <summary>Name and base currency of a legal entity.</summary>
public sealed record LegalEntityNameAndCurrency(Guid Id, string Name, string BaseCurrency);
