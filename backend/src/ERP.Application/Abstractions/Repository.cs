using System.Linq.Expressions;
using ERP.Application.Common;
using ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Abstractions;

/// <summary>A reusable query shape (criteria, includes, ordering). Compose instead of writing per-entity repositories.</summary>
public interface ISpecification<T> where T : class
{
    IQueryable<T> Apply(IQueryable<T> query);
}

/// <summary>Base specification: <c>new Specification&lt;Party&gt;(p =&gt; p.IsActive).Include(p =&gt; p.Something)</c>.</summary>
public class Specification<T>(Expression<Func<T, bool>>? criteria = null) : ISpecification<T> where T : class
{
    private readonly List<Expression<Func<T, object>>> _includes = new();
    public Expression<Func<T, bool>>? Criteria { get; } = criteria;

    public Specification<T> Include(Expression<Func<T, object>> include) { _includes.Add(include); return this; }

    public virtual IQueryable<T> Apply(IQueryable<T> query)
    {
        foreach (var include in _includes) query = query.Include(include);
        return Criteria is null ? query : query.Where(Criteria);
    }
}

/// <summary>Read side of the generic repository. Tenant, legal-entity and soft-delete filters always apply.</summary>
public interface IReadRepository<T> where T : Entity
{
    /// <summary>No-tracking queryable for ad-hoc reads and projections.</summary>
    IQueryable<T> Query(ISpecification<T>? spec = null);
    Task<T?> FindAsync(Guid id, CancellationToken ct = default);
    /// <summary>Like <see cref="FindAsync"/> but throws <see cref="NotFoundException"/>.</summary>
    Task<T> GetAsync(Guid id, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task<List<T>> ListAsync(ISpecification<T>? spec = null, CancellationToken ct = default);
    /// <summary>Generic search / filter / sort / page driven by the entity's [Searchable]/[Filterable]/[Sortable] whitelist.</summary>
    Task<PagedResult<T>> PageAsync(PagedQuery query, ISpecification<T>? spec = null, CancellationToken ct = default);
    Task<PagedResult<TDto>> PageAsync<TDto>(PagedQuery query, Expression<Func<T, TDto>> projection, ISpecification<T>? spec = null, CancellationToken ct = default);
}

/// <summary>Write side: tracked loads, add / remove and the unit-of-work commit (one DbContext per request).</summary>
public interface IRepository<T> : IReadRepository<T> where T : Entity
{
    /// <summary>Tracked load for updates; throws <see cref="NotFoundException"/>.</summary>
    Task<T> GetForUpdateAsync(Guid id, CancellationToken ct = default);
    IQueryable<T> Tracked(ISpecification<T>? spec = null);
    void Add(T entity);
    void AddRange(IEnumerable<T> entities);
    /// <summary>Soft-deletes ISoftDelete rows (via the save interceptor); hard-deletes the rest.</summary>
    void Remove(T entity);
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Forgets every tracked change and entity so a failed unit of work can be run again from a clean state.</summary>
    void DiscardChanges();
}

/// <summary>Commits all pending changes of the request (shared DbContext).</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Forgets every tracked change and entity so a failed unit of work can be run again from a clean state.</summary>
    void DiscardChanges();

    /// <summary>
    /// Runs <paramref name="work"/> in one database transaction (committed when it returns, rolled back when it throws).
    /// Use it when a command needs several SaveChanges that must succeed or fail together.
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}

/// <summary>
/// Non-generic access to any entity by CLR type, for features driven by runtime metadata (custom-field values, lookups)
/// where the entity type is only known from a registry. Tenant, legal-entity and soft-delete filters always apply.
/// </summary>
public interface IEntityStore
{
    Task<bool> ExistsAsync(Type entityType, Guid id, CancellationToken ct = default);
    Task<TEntity?> FindAsync<TEntity>(Guid id, CancellationToken ct = default) where TEntity : Entity;
    /// <summary>Tracked load for updates; null when the record is not visible to the caller.</summary>
    Task<Entity?> FindForUpdateAsync(Type entityType, Guid id, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Forgets every tracked change and entity so a failed unit of work can be run again from a clean state.</summary>
    void DiscardChanges();
}
