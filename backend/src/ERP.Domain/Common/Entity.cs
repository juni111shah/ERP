namespace ERP.Domain.Common;

/// <summary>Root of every persisted entity. Ids are GUIDs (sequential on SQL Server).</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = SequentialGuid.NewGuid();
}

/// <summary>Created/updated stamps, filled by the audit interceptor.</summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}

/// <summary>Rows owned by one organization (tenant). A global query filter restricts reads to the caller's tenant.</summary>
public interface IOrgScoped
{
    Guid OrgId { get; set; }
}

/// <summary>Rows that belong to a legal entity (the accounting boundary) inside a tenant.</summary>
public interface ILegalEntityScoped
{
    Guid LegalEntityId { get; set; }
}

/// <summary>Soft-deletable rows. Deletes become updates; a global filter hides deleted rows.</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
}

/// <summary>
/// Entities that accept user-defined fields (Odoo-Studio style). Values live in a JSON object column
/// keyed by <c>CustomFieldDefinition.Key</c>; definitions describe type and validation.
/// </summary>
public interface IHasCustomFields
{
    string? CustomFields { get; set; }
}

/// <summary>Base class for business tables: tenant, audit stamps, soft delete and optimistic concurrency.</summary>
public abstract class OrgEntity : Entity, IOrgScoped, IAuditable, ISoftDelete
{
    public Guid OrgId { get; set; }
    [Sortable, Filterable]
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    [Sortable]
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    /// <summary>SQL Server rowversion used for optimistic concurrency.</summary>
    public byte[]? RowVersion { get; set; }
}

/// <summary>Base class for rows scoped to a legal entity.</summary>
public abstract class LegalEntityScopedEntity : OrgEntity, ILegalEntityScoped
{
    [Filterable]
    public Guid LegalEntityId { get; set; }
}

public class DomainException(string message) : Exception(message);

/// <summary>
/// Time-ordered GUIDs that sort correctly in SQL Server uniqueidentifier indexes (the last 6 bytes hold a
/// timestamp), so inserts append instead of fragmenting the clustered index.
/// </summary>
public static class SequentialGuid
{
    private static long _last;

    public static Guid NewGuid()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        long ticks = DateTime.UtcNow.Ticks / 100; // 10 µs units: 48 bits last ~89 years
        long last, next;
        do
        {
            last = Interlocked.Read(ref _last);
            next = Math.Max(ticks, last + 1);
        } while (Interlocked.CompareExchange(ref _last, next, last) != last);
        var t = BitConverter.GetBytes(next);
        if (BitConverter.IsLittleEndian) Array.Reverse(t);
        // SQL Server compares bytes 10..15 first: put the 6 most significant changing bytes there.
        Array.Copy(t, 2, bytes, 10, 6);
        return new Guid(bytes);
    }
}
