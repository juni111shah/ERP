using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.IndustryPacks.Domain;

namespace ERP.Modules.IndustryPacks.Repositories;

/// <summary>Data access for the records of the industry packs.</summary>
public interface IIndustryRecordRepository : IRepository<IndustryRecord>
{
    /// <summary>The record of one module and type with its lines and events, read-only. Throws NotFoundException.</summary>
    Task<IndustryRecord> GetWithDetailsAsync(string module, string type, Guid id, CancellationToken ct);

    /// <summary>The record of one module and type with its lines and events, tracked for changes. Throws NotFoundException.</summary>
    Task<IndustryRecord> GetWithDetailsForUpdateAsync(string module, string type, Guid id, CancellationToken ct);

    /// <summary>A page of the records of one module and type.</summary>
    Task<PagedResult<IndustryRecord>> PageOfTypeAsync(string module, string type, PagedQuery paging, CancellationToken ct);

    /// <summary>Type, status and amount of every record of the module of the given types, of one legal entity or (null) all of them.</summary>
    Task<List<RecordStatusAmount>> ListStatusAmountsAsync(string module, string[] types, Guid? legalEntityId, CancellationToken ct);

    /// <summary>The record of the module and parent type that a record can belong to, or null.</summary>
    Task<RecordParent?> FindParentAsync(Guid parentId, string module, string parentType, CancellationToken ct);

    /// <summary>A record with its lines, tracked for changes, or null.</summary>
    Task<IndustryRecord?> FindWithLinesForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The record a vendor bill was raised from, tracked for changes, or null.</summary>
    Task<IndustryRecord?> FindByBillForUpdateAsync(Guid billId, CancellationToken ct);
}

/// <summary>Type, status and amount of one record.</summary>
public sealed record RecordStatusAmount(string RecordType, string Status, decimal Amount);

/// <summary>What a record keeps of the record it belongs to.</summary>
public sealed record RecordParent(Guid Id, string Number, string Title, Guid LegalEntityId);
