using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for properties. Implemented in PropertyRepository.cs.</summary>
public interface IPropertyRepository : IRepository<Property>
{
    /// <summary>The name of each of the properties, by id.</summary>
    Task<Dictionary<Guid, string>> NamesAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The name of a property; null when not found.</summary>
    Task<string?> FindNameAsync(Guid id, CancellationToken ct);

    /// <summary>The active properties of one legal entity or (null) all, by name, with their lettable unit count.</summary>
    Task<List<PropertyUnitCount>> ListActiveUnitCountsAsync(Guid? legalEntityId, CancellationToken ct);

    /// <summary>The active properties of one legal entity or (null) all, by name, with their indicative value.</summary>
    Task<List<PropertyValue>> ListActiveValuesAsync(Guid? legalEntityId, CancellationToken ct);
}

/// <summary>A property and the number of units it can let.</summary>
public sealed record PropertyUnitCount(Guid Id, string Name, Guid LegalEntityId, int TotalUnits);

/// <summary>A property and its indicative value.</summary>
public sealed record PropertyValue(Guid Id, string Name, Guid LegalEntityId, decimal IndicativeValue);
