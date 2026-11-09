using System.Linq.Expressions;
using System.Reflection;
using ERP.Application.Common;
using ERP.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Application.Abstractions;

/// <summary>
/// Base of every entity repository (<c>InvoiceRepository : RepositoryBase&lt;Invoice&gt;, IInvoiceRepository</c>).
/// <para>
/// It inherits the generic operations (<c>GetAsync</c>, <c>Add</c>, <c>SaveChangesAsync</c>, paging...) from the generic
/// repository, so tenant, legal-entity and soft-delete filters always apply. The entity repository adds <b>named</b> queries
/// (<c>GetWithLinesAsync</c>, <c>TotalCreditedAsync</c>...) so handlers read as plain steps and keep no LINQ of their own.
/// </para>
/// Repositories are registered automatically (see <see cref="RepositoryRegistration"/>).
/// </summary>
public abstract class RepositoryBase<T>(IRepository<T> inner) : IRepository<T> where T : Entity
{
    public IQueryable<T> Query(ISpecification<T>? spec = null) => inner.Query(spec);
    public IQueryable<T> Tracked(ISpecification<T>? spec = null) => inner.Tracked(spec);
    public Task<T?> FindAsync(Guid id, CancellationToken ct = default) => inner.FindAsync(id, ct);
    public Task<T> GetAsync(Guid id, CancellationToken ct = default) => inner.GetAsync(id, ct);
    public Task<T> GetForUpdateAsync(Guid id, CancellationToken ct = default) => inner.GetForUpdateAsync(id, ct);
    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => inner.AnyAsync(predicate, ct);
    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) => inner.CountAsync(predicate, ct);
    public Task<List<T>> ListAsync(ISpecification<T>? spec = null, CancellationToken ct = default) => inner.ListAsync(spec, ct);
    public Task<PagedResult<T>> PageAsync(PagedQuery query, ISpecification<T>? spec = null, CancellationToken ct = default) => inner.PageAsync(query, spec, ct);
    public Task<PagedResult<TDto>> PageAsync<TDto>(PagedQuery query, Expression<Func<T, TDto>> projection, ISpecification<T>? spec = null, CancellationToken ct = default)
        => inner.PageAsync(query, projection, spec, ct);
    public void Add(T entity) => inner.Add(entity);
    public void AddRange(IEnumerable<T> entities) => inner.AddRange(entities);
    public void Remove(T entity) => inner.Remove(entity);
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => inner.SaveChangesAsync(ct);
    public void DiscardChanges() => inner.DiscardChanges();
}

/// <summary>Registers every concrete <see cref="RepositoryBase{T}"/> of the given assemblies against its own interfaces (scoped).</summary>
public static class RepositoryRegistration
{
    public static IServiceCollection AddRepositories(this IServiceCollection services, params Assembly[] assemblies)
    {
        var generic = new[] { typeof(IRepository<>), typeof(IReadRepository<>) };
        foreach (var type in assemblies.Distinct().SelectMany(a => a.GetTypes()).Where(t => t is { IsClass: true, IsAbstract: false } && IsRepository(t)))
        {
            services.AddScoped(type);
            foreach (var contract in type.GetInterfaces().Where(i => !(i.IsGenericType && generic.Contains(i.GetGenericTypeDefinition()))))
                services.AddScoped(contract, sp => sp.GetRequiredService(type));
        }
        return services;
    }

    private static bool IsRepository(Type type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(RepositoryBase<>)) return true;
        return false;
    }
}
