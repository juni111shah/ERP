using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Common;

/// <summary>
/// Standard list request: <c>?page=1&amp;pageSize=25&amp;sort=name,-createdAt&amp;search=acme&amp;filter=isActive:eq:true</c>.
/// Derive from it for lists with extra parameters (<c>record ListParties : PagedQuery</c>).
/// </summary>
public record PagedQuery
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    public int? Page { get; init; }
    public int? PageSize { get; init; }
    /// <summary>Comma-separated sortable fields; prefix with '-' for descending.</summary>
    public string? Sort { get; init; }
    /// <summary>Free text over [Searchable] properties.</summary>
    public string? Search { get; init; }
    /// <summary>Repeatable <c>field:op:value</c> over [Filterable] properties. Ops: eq ne gt gte lt lte contains startsWith in has isNull isNotNull.</summary>
    public string[]? Filter { get; init; }

    public int PageNumber => Math.Max(1, Page ?? 1);
    public int Size => Math.Clamp(PageSize ?? DefaultPageSize, 1, MaxPageSize);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize) : IPagedResult
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;

    System.Collections.IEnumerable IPagedResult.Items => Items;

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) => new(Items.Select(map).ToList(), Total, Page, PageSize);
}

/// <summary>Marker used by the API envelope to recognise paged payloads without reflection on generic types.</summary>
public interface IPagedResult
{
    System.Collections.IEnumerable Items { get; }
    int Total { get; }
    int Page { get; }
    int PageSize { get; }
}

/// <summary>Reflected whitelist of an entity's list capabilities (cached per type).</summary>
public sealed class ListMetadata
{
    private static readonly ConcurrentDictionary<Type, ListMetadata> Cache = new();

    public IReadOnlyDictionary<string, PropertyInfo> Searchable { get; }
    public IReadOnlyDictionary<string, PropertyInfo> Sortable { get; }
    public IReadOnlyDictionary<string, PropertyInfo> Filterable { get; }
    public string? DefaultSort { get; }

    private ListMetadata(Type t)
    {
        var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        bool Has<TA>(PropertyInfo p) where TA : Attribute => p.GetCustomAttribute<TA>(true) is not null || p.GetCustomAttribute<ListableAttribute>(true) is not null;
        Dictionary<string, PropertyInfo> Pick(Func<PropertyInfo, bool> f) => props.Where(f).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        Searchable = Pick(p => p.PropertyType == typeof(string) && Has<SearchableAttribute>(p));
        Sortable = Pick(Has<SortableAttribute>);
        Filterable = Pick(Has<FilterableAttribute>);
        DefaultSort = t.GetCustomAttribute<DefaultSortAttribute>(true)?.Sort;
    }

    public static ListMetadata For(Type t) => Cache.GetOrAdd(t, x => new ListMetadata(x));
    public static ListMetadata For<T>() => For(typeof(T));
}

/// <summary>Generic, whitelist-driven search / filter / sort / paging for any IQueryable.</summary>
public static class QueryableListExtensions
{
    private static readonly MethodInfo StringContains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
    private static readonly MethodInfo StringStartsWith = typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;

    public static IQueryable<T> ApplySearch<T>(this IQueryable<T> source, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return source;
        var props = ListMetadata.For<T>().Searchable.Values.ToList();
        if (props.Count == 0) return source;
        var x = Expression.Parameter(typeof(T), "x");
        Expression? body = null;
        foreach (var term in search.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(5))
        {
            var value = Captured(term, typeof(string));
            Expression? any = null;
            foreach (var p in props)
            {
                var member = Expression.Property(x, p);
                var test = Expression.AndAlso(Expression.NotEqual(member, Expression.Constant(null, typeof(string))), Expression.Call(member, StringContains, value));
                any = any is null ? test : Expression.OrElse(any, test);
            }
            body = body is null ? any : Expression.AndAlso(body, any!);
        }
        return source.Where(Expression.Lambda<Func<T, bool>>(body!, x));
    }

    public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> source, IEnumerable<string>? filters)
    {
        if (filters is null) return source;
        var meta = ListMetadata.For<T>();
        foreach (var raw in filters.Where(f => !string.IsNullOrWhiteSpace(f)))
        {
            var parts = raw.Split(':', 3);
            if (parts.Length < 2) throw new FieldValidationException("filter", $"'{raw}' is not field:op:value.");
            if (!meta.Filterable.TryGetValue(parts[0], out var prop)) throw new FieldValidationException("filter", $"'{parts[0]}' cannot be filtered.");
            var x = Expression.Parameter(typeof(T), "x");
            var predicate = BuildPredicate(Expression.Property(x, prop), parts[1], parts.Length > 2 ? parts[2] : null, parts[0]);
            source = source.Where(Expression.Lambda<Func<T, bool>>(predicate, x));
        }
        return source;
    }

    public static IQueryable<T> ApplySort<T>(this IQueryable<T> source, string? sort)
    {
        var meta = ListMetadata.For<T>();
        sort = string.IsNullOrWhiteSpace(sort) ? meta.DefaultSort : sort;
        var tokens = (sort ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        IOrderedQueryable<T>? ordered = null;
        foreach (var token in tokens)
        {
            var desc = token.StartsWith('-');
            var name = token.TrimStart('-', '+');
            if (!meta.Sortable.TryGetValue(name, out var prop)) throw new FieldValidationException("sort", $"'{name}' cannot be sorted.");
            ordered = OrderBy(ordered ?? source, prop, desc, ordered is not null);
        }
        // Stable paging: always end with the key.
        var id = typeof(T).GetProperty(nameof(Entity.Id));
        if (id is not null) ordered = OrderBy(ordered ?? source, id, false, ordered is not null);
        return ordered ?? source;
    }

    public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> source, int page, int pageSize) =>
        source.Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize);

    /// <summary>search → filters → count → sort → page.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> source, PagedQuery q, CancellationToken ct = default)
    {
        var filtered = source.ApplySearch(q.Search).ApplyFilters(q.Filter);
        var total = await filtered.CountAsync(ct);
        var rows = await filtered.ApplySort(q.Sort).ApplyPaging(q.PageNumber, q.Size).ToListAsync(ct);
        return new PagedResult<T>(rows, total, q.PageNumber, q.Size);
    }

    /// <summary>Same as <see cref="ToPagedResultAsync{T}"/> with a server-side projection (only the DTO columns are read).</summary>
    public static async Task<PagedResult<TDto>> ToPagedResultAsync<T, TDto>(this IQueryable<T> source, PagedQuery q, Expression<Func<T, TDto>> projection, CancellationToken ct = default)
    {
        var filtered = source.ApplySearch(q.Search).ApplyFilters(q.Filter);
        var total = await filtered.CountAsync(ct);
        var rows = await filtered.ApplySort(q.Sort).ApplyPaging(q.PageNumber, q.Size).Select(projection).ToListAsync(ct);
        return new PagedResult<TDto>(rows, total, q.PageNumber, q.Size);
    }

    private static IOrderedQueryable<T> OrderBy<T>(IQueryable<T> source, PropertyInfo prop, bool desc, bool then)
    {
        var x = Expression.Parameter(typeof(T), "x");
        var key = Expression.Lambda(Expression.Property(x, prop), x);
        var method = (then, desc) switch
        {
            (false, false) => nameof(Queryable.OrderBy),
            (false, true) => nameof(Queryable.OrderByDescending),
            (true, false) => nameof(Queryable.ThenBy),
            _ => nameof(Queryable.ThenByDescending),
        };
        var call = Expression.Call(typeof(Queryable), method, [typeof(T), prop.PropertyType], source.Expression, Expression.Quote(key));
        return (IOrderedQueryable<T>)source.Provider.CreateQuery<T>(call);
    }

    private static Expression BuildPredicate(MemberExpression member, string op, string? raw, string field)
    {
        var type = member.Type;
        var core = Nullable.GetUnderlyingType(type) ?? type;
        switch (op.ToLowerInvariant())
        {
            case "isnull" or "isnotnull":
                if (type.IsValueType && Nullable.GetUnderlyingType(type) is null)
                    throw new FieldValidationException("filter", $"'{field}' is never empty.");
                return op.Equals("isnull", StringComparison.OrdinalIgnoreCase)
                    ? Expression.Equal(member, Expression.Constant(null, type))
                    : Expression.NotEqual(member, Expression.Constant(null, type));
            case "contains" when core == typeof(string):
                return Expression.AndAlso(Expression.NotEqual(member, Expression.Constant(null, type)), Expression.Call(member, StringContains, Captured(raw ?? "", typeof(string))));
            case "startswith" when core == typeof(string):
                return Expression.AndAlso(Expression.NotEqual(member, Expression.Constant(null, type)), Expression.Call(member, StringStartsWith, Captured(raw ?? "", typeof(string))));
            case "in":
            {
                var values = (raw ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries).Select(v => Parse(v, core, field)).ToList();
                var list = typeof(List<>).MakeGenericType(type);
                var typed = (System.Collections.IList)Activator.CreateInstance(list)!;
                foreach (var v in values) typed.Add(v);
                return Expression.Call(Captured(typed, list), list.GetMethod("Contains")!, member);
            }
            case "has" when core.IsEnum && core.GetCustomAttribute<FlagsAttribute>() is not null:
            {
                var flag = Convert.ToInt64(Parse(raw ?? "", core, field), CultureInfo.InvariantCulture);
                var asLong = Expression.Convert(member, typeof(long));
                var value = Captured(flag, typeof(long));
                return Expression.Equal(Expression.And(asLong, value), value);
            }
        }
        var constant = Captured(Parse(raw ?? "", core, field), type);
        return op.ToLowerInvariant() switch
        {
            "eq" => Expression.Equal(member, constant),
            "ne" => Expression.NotEqual(member, constant),
            "gt" when Comparable(core) => Expression.GreaterThan(member, constant),
            "gte" when Comparable(core) => Expression.GreaterThanOrEqual(member, constant),
            "lt" when Comparable(core) => Expression.LessThan(member, constant),
            "lte" when Comparable(core) => Expression.LessThanOrEqual(member, constant),
            _ => throw new FieldValidationException("filter", $"Operator '{op}' is not supported for '{field}'."),
        };
    }

    private static bool Comparable(Type t) => t != typeof(string) && t != typeof(bool) && t != typeof(Guid) && !t.IsEnum;

    private static object? Parse(string raw, Type t, string field)
    {
        try
        {
            if (t == typeof(string)) return raw;
            if (t == typeof(Guid)) return Guid.Parse(raw);
            if (t.IsEnum) return Enum.Parse(t, raw.Replace('|', ','), ignoreCase: true);
            if (t == typeof(bool)) return bool.Parse(raw);
            if (t == typeof(DateTime)) return DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            if (t == typeof(DateOnly)) return DateOnly.Parse(raw, CultureInfo.InvariantCulture);
            return Convert.ChangeType(raw, t, CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is FormatException or ArgumentException or InvalidCastException or OverflowException)
        {
            throw new FieldValidationException("filter", $"'{raw}' is not a valid value for '{field}'.");
        }
    }

    /// <summary>Wraps a value as a closure member so EF Core sends it as a SQL parameter (not an inlined constant).</summary>
    private static Expression Captured(object? value, Type type)
    {
        var box = new ValueBox(value);
        return Expression.Convert(Expression.Field(Expression.Constant(box), nameof(ValueBox.Value)), type);
    }

    private sealed class ValueBox(object? value)
    {
        public readonly object? Value = value;
    }
}
