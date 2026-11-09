using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class PropertyRepository(IRepository<Property> inner) : RepositoryBase<Property>(inner), IPropertyRepository
{
    public Task<Dictionary<Guid, string>> NamesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Property>(p => ids.Contains(p.Id))).Select(p => new { p.Id, p.Name }).ToDictionaryAsync(p => p.Id, p => p.Name, ct);

    public Task<string?> FindNameAsync(Guid id, CancellationToken ct)
        => Query(new Specification<Property>(p => p.Id == id)).Select(p => p.Name).FirstOrDefaultAsync(ct);

    public Task<List<PropertyUnitCount>> ListActiveUnitCountsAsync(Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<Property>(p => p.IsActive && (legalEntityId == null || p.LegalEntityId == legalEntityId))).OrderBy(p => p.Name)
            .Select(p => new PropertyUnitCount(p.Id, p.Name, p.LegalEntityId, p.TotalUnits)).ToListAsync(ct);

    public Task<List<PropertyValue>> ListActiveValuesAsync(Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<Property>(p => p.IsActive && (legalEntityId == null || p.LegalEntityId == legalEntityId))).OrderBy(p => p.Name)
            .Select(p => new PropertyValue(p.Id, p.Name, p.LegalEntityId, p.IndicativeValue)).ToListAsync(ct);
}
