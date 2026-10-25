using System.Linq.Expressions;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

/// <summary>One EF Core implementation for every entity (registered as an open generic). No per-entity repositories.</summary>
public sealed class EfRepository<T>(AppDbContext db) : IRepository<T> where T : Entity
{
    private DbSet<T> Set => db.Set<T>();

    public IQueryable<T> Query(ISpecification<T>? spec = null) => spec?.Apply(Set.AsNoTracking()) ?? Set.AsNoTracking();
    public IQueryable<T> Tracked(ISpecification<T>? spec = null) => spec?.Apply(Set) ?? Set;

    public Task<T?> FindAsync(Guid id, CancellationToken ct = default) => Set.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<T> GetAsync(Guid id, CancellationToken ct = default) => await FindAsync(id, ct) ?? throw new NotFoundException();
    public async Task<T> GetForUpdateAsync(Guid id, CancellationToken ct = default) => await Set.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException();

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => Set.AnyAsync(predicate, ct);
    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) => predicate is null ? Set.CountAsync(ct) : Set.CountAsync(predicate, ct);
    public Task<List<T>> ListAsync(ISpecification<T>? spec = null, CancellationToken ct = default) => Query(spec).ToListAsync(ct);

    public Task<PagedResult<T>> PageAsync(PagedQuery query, ISpecification<T>? spec = null, CancellationToken ct = default) =>
        Query(spec).ToPagedResultAsync(query, ct);

    public Task<PagedResult<TDto>> PageAsync<TDto>(PagedQuery query, Expression<Func<T, TDto>> projection, ISpecification<T>? spec = null, CancellationToken ct = default) =>
        Query(spec).ToPagedResultAsync(query, projection, ct);

    public void Add(T entity) => Set.Add(entity);
    public void AddRange(IEnumerable<T> entities) => Set.AddRange(entities);
    public void Remove(T entity) => Set.Remove(entity);
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

/// <summary>EF Core implementation of <see cref="IEntityStore"/> (one reflection hop per call, only for metadata-driven features).</summary>
public sealed class EfEntityStore(AppDbContext db) : IEntityStore
{
    private IQueryable<Entity> Set(Type entityType) =>
        (IQueryable<Entity>)typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(entityType).Invoke(db, null)!;

    public Task<bool> ExistsAsync(Type entityType, Guid id, CancellationToken ct = default) => Set(entityType).AnyAsync(e => e.Id == id, ct);
    public Task<TEntity?> FindAsync<TEntity>(Guid id, CancellationToken ct = default) where TEntity : Entity =>
        db.Set<TEntity>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
    public async Task<Entity?> FindForUpdateAsync(Type entityType, Guid id, CancellationToken ct = default) => await Set(entityType).FirstOrDefaultAsync(e => e.Id == id, ct);
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
