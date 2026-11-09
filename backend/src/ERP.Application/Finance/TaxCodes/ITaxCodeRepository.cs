using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>A tax code's code, name and rate.</summary>
public sealed record TaxCodeLabel(Guid Id, string Code, string Name, decimal Rate);

/// <summary>Data access for tax codes. Implemented in ERP.Infrastructure/Repositories/Finance/TaxCodeRepository.cs.</summary>
public interface ITaxCodeRepository : IRepository<TaxCode>
{
    /// <summary>Code, name and rate of every tax code, by id.</summary>
    Task<Dictionary<Guid, TaxCodeLabel>> MapLabelsAsync(CancellationToken ct);

    /// <summary>The other tax codes flagged as the default, tracked for changes.</summary>
    Task<List<TaxCode>> ListOtherDefaultsForUpdateAsync(Guid exceptId, CancellationToken ct);

    /// <summary>Rate and recoverability of those of the tax codes that are active, by id.</summary>
    Task<Dictionary<Guid, TaxCodeRate>> ActiveRatesAsync(List<Guid> ids, CancellationToken ct);
}

/// <summary>A tax code's rate (percent) and whether the tax can be recovered.</summary>
public sealed record TaxCodeRate(Guid Id, decimal Rate, bool IsRecoverable);
